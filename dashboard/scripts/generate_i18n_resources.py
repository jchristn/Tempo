from __future__ import annotations

import json
import re
import subprocess
from pathlib import Path

import argostranslate.translate


ROOT = Path(__file__).resolve().parents[1]
I18N_DIR = ROOT / "src" / "i18n"
OUTPUT_FILE = I18N_DIR / "generatedResources.js"
CACHE_FILE = ROOT / "scripts" / ".generatedResources.cache.json"

TARGETS = {
    "es": "es",
    "zh-Hans": "zh",
    "yue-Hant-HK": "zt",
    "ja": "ja",
    "de": "de",
    "fr": "fr",
    "it": "it",
    "zh-Hant-TW": "zt",
}

PLACEHOLDER_PATTERN = re.compile(r"\{\{[^}]+\}\}")
TRANSLATORS: dict[str, object] = {}


def run_node_extractor() -> dict:
    script = r"""
import { resources } from './src/i18n/resources.js';
import { extractTranslatableStrings } from './src/i18n/extractTranslatableStrings.js';

console.log(JSON.stringify({
  en: resources.en.translation,
  ...extractTranslatableStrings()
}));
"""
    result = subprocess.run(
        ["node", "--input-type=module", "-e", script],
        cwd=ROOT,
        capture_output=True,
        text=True,
        encoding="utf-8",
        check=True,
    )
    return json.loads(result.stdout)


def flatten(obj: dict, prefix: str = "", out: dict | None = None) -> dict:
    if out is None:
        out = {}
    for key, value in (obj or {}).items():
        next_key = f"{prefix}.{key}" if prefix else key
        if isinstance(value, dict):
            flatten(value, next_key, out)
        else:
            out[next_key] = value
    return out


def unflatten(flat: dict[str, str]) -> dict:
    root: dict = {}
    for key, value in flat.items():
        parts = key.split(".")
        cursor = root
        for part in parts[:-1]:
            cursor = cursor.setdefault(part, {})
        cursor[parts[-1]] = value
    return root


def deep_merge(base: dict, override: dict) -> dict:
    result = dict(base)
    for key, value in override.items():
        if isinstance(value, dict) and isinstance(result.get(key), dict):
            result[key] = deep_merge(result[key], value)
        else:
            result[key] = value
    return result


def protect_placeholders(text: str) -> tuple[str, dict[str, str]]:
    replacements: dict[str, str] = {}

    def repl(match: re.Match[str]) -> str:
        token = f"__PH_{len(replacements)}__"
        replacements[token] = match.group(0)
        return token

    protected = PLACEHOLDER_PATTERN.sub(repl, text)
    return protected, replacements


def restore_placeholders(text: str, replacements: dict[str, str]) -> str:
    restored = text
    for token, original in replacements.items():
        restored = restored.replace(token, original)
    return restored


def load_cache() -> dict[str, dict[str, str]]:
    if not CACHE_FILE.exists():
        return {}
    return json.loads(CACHE_FILE.read_text(encoding="utf-8"))


def save_cache(cache: dict[str, dict[str, str]]) -> None:
    CACHE_FILE.write_text(json.dumps(cache, ensure_ascii=False, indent=2), encoding="utf-8")


def get_translator(target_code: str):
    if target_code in TRANSLATORS:
        return TRANSLATORS[target_code]

    installed = {language.code: language for language in argostranslate.translate.get_installed_languages()}
    source = installed.get("en")
    target = installed.get(target_code)
    if source is None or target is None:
        raise RuntimeError(f"Missing Argos translation package for en -> {target_code}")

    translator = source.get_translation(target)
    if translator is None:
        raise RuntimeError(f"Unable to create Argos translator for en -> {target_code}")

    TRANSLATORS[target_code] = translator
    return translator


def translate_texts(texts: list[str], target_code: str, cache: dict[str, dict[str, str]]) -> dict[str, str]:
    target_cache = cache.setdefault(target_code, {})
    missing = [text for text in texts if text not in target_cache]

    if missing:
        translator = get_translator(target_code)
        for index, text in enumerate(missing, start=1):
            protected, replacements = protect_placeholders(text)
            translated = translator.translate(protected) or text
            target_cache[text] = restore_placeholders(str(translated), replacements)
            if index % 50 == 0 or index == len(missing):
                print(f"[{target_code}] translated {index}/{len(missing)}")
        save_cache(cache)

    return {text: target_cache[text] for text in texts}


def to_js(value) -> str:
    return json.dumps(value, ensure_ascii=False, indent=2)


def build_generated_resources() -> dict:
    extracted = run_node_extractor()
    flat_en = flatten(extracted["en"])
    literals: list[str] = extracted["literals"]
    keyed_defaults: dict[str, str] = extracted["keyedDefaults"]

    texts = sorted(
        {
            *[str(v) for v in flat_en.values() if isinstance(v, str)],
            *[str(v) for v in keyed_defaults.values() if isinstance(v, str)],
            *[str(v) for v in literals if isinstance(v, str)],
        }
    )

    print(f"unique translatable texts: {len(texts)}")

    cache = load_cache()
    generated: dict[str, dict] = {}

    for locale, target_code in TARGETS.items():
        translated = translate_texts(texts, target_code, cache)
        structured = {key: translated.get(value, value) for key, value in flat_en.items()}
        default_keys = {key: translated.get(value, value) for key, value in keyed_defaults.items()}
        literal_map = {value: translated.get(value, value) for value in literals}

        locale_object = deep_merge(unflatten(structured), unflatten(default_keys))
        locale_object.update(literal_map)
        generated[locale] = {"translation": locale_object}

    return generated


def main() -> None:
    generated = build_generated_resources()
    OUTPUT_FILE.write_text(
        "// Auto-generated by scripts/generate_i18n_resources.py\n"
        "export const generatedLocaleResources = "
        + to_js(generated)
        + ";\n\nexport default generatedLocaleResources;\n",
        encoding="utf-8",
    )
    print(f"wrote {OUTPUT_FILE}")


if __name__ == "__main__":
    main()
