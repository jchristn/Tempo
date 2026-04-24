import generatedLocaleResources from './generatedResources.js';

function deepMerge(base, override) {
  if (override === undefined) return Array.isArray(base) ? [...base] : { ...base };
  if (Array.isArray(base) || Array.isArray(override)) return override;

  const result = { ...(base || {}) };
  for (const [key, value] of Object.entries(override || {})) {
    if (value && typeof value === 'object' && !Array.isArray(value) && base && typeof base[key] === 'object' && !Array.isArray(base[key])) {
      result[key] = deepMerge(base[key], value);
    } else {
      result[key] = value;
    }
  }
  return result;
}

const en = {
  common: {
    appName: 'Tempo',
    language: {
      label: 'Language',
      selector: 'Choose dashboard language'
    },
    actions: {
      add: 'Add',
      addHeader: 'Add header',
      addRow: 'Add row',
      actions: 'Actions',
      back: 'Back',
      cancel: 'Cancel',
      clear: 'Clear',
      close: 'Close',
      confirm: 'Confirm',
      copy: 'Copy to clipboard',
      copied: 'Copied!',
      create: 'Create',
      delete: 'Delete',
      deleteAll: 'Delete all',
      download: 'Download',
      done: 'Done',
      edit: 'Edit',
      execute: 'Execute',
      first: 'First',
      last: 'Last',
      next: 'Next',
      previous: 'Previous',
      refresh: 'Refresh',
      reload: 'Reload',
      remove: 'Remove',
      reset: 'Reset',
      save: 'Save',
      saveAll: 'Save all',
      search: 'Search',
      signIn: 'Sign in',
      signOut: 'Sign out',
      skipSetup: 'Skip setup',
      startSetup: 'Start setup',
      view: 'View',
      viewJson: 'View JSON'
    },
    generic: {
      active: 'Active',
      any: 'Any',
      empty: '(empty)',
      loading: 'Loading...',
      none: '(none)',
      notAvailable: 'Unavailable',
      optional: 'optional',
      terminate: '(terminate)',
      unknown: 'Unknown'
    },
    boolean: {
      yes: 'Yes',
      no: 'No'
    },
    theme: {
      light: 'light',
      dark: 'dark',
      toggle: 'Toggle theme',
      switchTo: 'Switch to {{theme}} theme',
      switchMode: 'Switch to {{theme}} mode'
    },
    direction: {
      ltr: 'ltr',
      rtl: 'rtl'
    },
    placeholders: {
      dash: '-',
      ellipsis: '...'
    },
    units: {
      millisecondsShort: 'ms',
      secondsShort: 's',
      minutesShort: 'm',
      bytesShort: 'B',
      kilobytesShort: 'KB',
      megabytesShort: 'MB',
      gigabytesShort: 'GB'
    },
    relativeTime: {
      now: 'now'
    }
  },
  navigation: {
    sections: {
      observability: 'Observability',
      flows: 'Flows',
      administration: 'Administration',
      system: 'System'
    },
    items: {
      home: {
        label: 'Home',
        tip: 'Activity and health at a glance'
      },
      requests: {
        label: 'Request History',
        tip: 'Every captured HTTP request, with bucketed activity chart'
      },
      explorer: {
        label: 'API Explorer',
        tip: 'Browse and exercise every endpoint discovered from /openapi.json'
      },
      steps: {
        label: 'Steps',
        tip: 'Create reusable units of work first'
      },
      flows: {
        label: 'Data Flows',
        tip: 'Connect steps into an executable graph next'
      },
      triggers: {
        label: 'Triggers',
        tip: 'Create inbound entry points after a flow exists'
      },
      runs: {
        label: 'Runs',
        tip: 'Review each execution and per-step timeline'
      },
      artifacts: {
        label: 'Artifacts',
        tip: 'Manage uploaded packages used by artifact-backed steps'
      },
      runtimes: {
        label: 'Runtimes',
        tip: 'Review runtime providers and tenant availability'
      },
      tenants: {
        label: 'Tenants',
        tip: 'Top-level isolation boundary; owns users, flows, runs, etc.'
      },
      users: {
        label: 'Users',
        tip: 'Tenant-scoped login accounts and their role assignments'
      },
      credentials: {
        label: 'Credentials',
        tip: 'API access key / secret key pairs used by integrations'
      },
      roles: {
        label: 'Roles',
        tip: 'Containers for granular permission grants'
      },
      permissions: {
        label: 'Permissions',
        tip: 'Granular permit/deny rules over resource + operation pairs'
      },
      workers: {
        label: 'Workers',
        tip: 'Inspect worker state, placement labels, and drain or resume nodes'
      },
      logs: {
        label: 'Logs',
        tip: 'Browse server and worker log files, read bounded tails, and download or clear logs'
      },
      settings: {
        label: 'Settings',
        tip: 'Server configuration and dashboard preferences'
      }
    },
    setupWizard: 'Setup wizard',
    openSetupWizard: 'Open setup wizard',
    version: 'Tempo version {{version}}',
    toggleSidebar: 'Toggle sidebar',
    github: 'View Tempo on GitHub',
    authenticatedPrincipal: 'Authenticated principal',
    serverAt: 'Tempo server at {{serverUrl}}'
  },
  login: {
    title: 'Tempo',
    subtitle: 'Data flow orchestration',
    serverUrl: 'Server URL',
    email: 'Email',
    password: 'Password',
    tenantId: 'Tenant ID',
    tenantOptional: '(optional)',
    tenantPlaceholder: 'Leave blank for administrator login',
    connecting: 'Connecting...',
    connectError: 'Failed to connect. Check the URL and credentials.',
    seededCredentials: 'Default seeded credentials: admin@tempo.local / password'
  },
  components: {
    table: {
      pagination: {
        groupLabel: 'Table pagination',
        records_one: '{{count}} record',
        records_other: '{{count}} records',
        perPage: 'Per page',
        page: 'Page',
        of: 'of',
        firstPage: 'First page',
        previousPage: 'Previous page',
        nextPage: 'Next page',
        lastPage: 'Last page'
      },
      selectAllRows: 'Select all rows',
      selectRow: 'Select row',
      empty: 'No items found',
      bulkSelected_one: '{{count}} selected',
      bulkSelected_other: '{{count}} selected',
      bulkDeleteLabel: 'Delete selected',
      bulkDeleteConfirm_one: 'Delete {{count}} selected item? This cannot be undone.',
      bulkDeleteConfirm_other: 'Delete {{count}} selected items? This cannot be undone.'
    },
    modal: {
      close: 'Close',
      id: 'ID',
      json: 'JSON'
    },
    requestDetails: {
      title: 'Request details',
      requestId: 'Request ID',
      metadata: 'Metadata',
      methodStatus: 'Method / Status',
      path: 'Path',
      url: 'URL',
      created: 'Created',
      completed: 'Completed',
      duration: 'Duration',
      sourceIp: 'Source IP',
      principal: 'Principal',
      tenant: 'Tenant',
      user: 'User',
      requestHeaders: 'Request headers',
      requestBody: 'Request body',
      responseHeaders: 'Response headers',
      responseBody: 'Response body',
      rawJson: 'Raw JSON',
      truncated: 'truncated',
      bytes: '{{count}} bytes'
    },
    chart: {
      total: 'Total',
      success: 'Success',
      failed: 'Failed',
      refresh: 'Refresh',
      activity: 'Activity',
      noActivity: 'No activity data for this time range',
      totalSeries: 'Total',
      successSeries: 'Success',
      failureSeries: 'Failed',
      lastHour: 'Last Hour',
      lastDay: 'Last Day',
      lastWeek: 'Last Week',
      lastMonth: 'Last Month'
    },
    graph: {
      newStepPlaceholder: 'New step id (e.g. validate)',
      addStep: '+ Add step',
      helper: 'Drag nodes to move. Drag from a colored port to connect. Green=success, amber=failure, red=exception.',
      removeEdge: 'Remove edge',
      onSuccess: 'On success ->',
      onFailure: 'On failure ->',
      onException: 'On exception ->',
      maxTransitions: 'Max transitions (0 = unlimited)',
      step: 'Step: {{stepId}}',
      portSuccess: 'OnSuccess - drag to connect',
      portFailure: 'OnFailure - drag to connect',
      portException: 'OnException - drag to connect'
    },
    topbar: {
      signOut: 'Sign out'
    }
  },
  views: {
    home: {
      title: 'Home',
      subtitle: 'Monitor request activity, runtime pressure, and recent health at a glance.',
      serverLogs: 'Server logs'
    },
    requestHistory: {
      title: 'Request History',
      subtitle: 'Audit captured HTTP requests, response codes, timings, and payload excerpts.'
    },
    apiExplorer: {
      title: 'API Explorer'
    },
    dataFlows: {
      title: 'Data Flows'
    },
    steps: {
      title: 'Steps'
    },
    triggers: {
      title: 'Triggers'
    },
    runs: {
      title: 'Runs'
    },
    artifacts: {
      title: 'Artifacts'
    },
    runtimes: {
      title: 'Runtimes'
    },
    tenants: {
      title: 'Tenants'
    },
    users: {
      title: 'Users'
    },
    credentials: {
      title: 'Credentials'
    },
    roles: {
      title: 'Roles'
    },
    permissions: {
      title: 'Permissions'
    },
    workers: {
      title: 'Workers'
    },
    logs: {
      title: 'Logs'
    },
    settings: {
      title: 'Settings'
    }
  }
};

const localeOverrides = {
  es: {
    common: {
      language: { label: 'Idioma', selector: 'Elegir idioma del panel' },
      actions: {
        actions: 'Acciones',
        back: 'Atrás',
        cancel: 'Cancelar',
        clear: 'Limpiar',
        close: 'Cerrar',
        copy: 'Copiar al portapapeles',
        copied: '¡Copiado!',
        create: 'Crear',
        delete: 'Eliminar',
        download: 'Descargar',
        done: 'Listo',
        edit: 'Editar',
        execute: 'Ejecutar',
        refresh: 'Actualizar',
        reload: 'Recargar',
        remove: 'Quitar',
        reset: 'Restablecer',
        save: 'Guardar',
        saveAll: 'Guardar todo',
        signIn: 'Iniciar sesión',
        signOut: 'Cerrar sesión',
        skipSetup: 'Omitir configuración',
        startSetup: 'Iniciar configuración',
        view: 'Ver',
        viewJson: 'Ver JSON'
      },
      generic: {
        active: 'Activo',
        any: 'Cualquiera',
        empty: '(vacío)',
        loading: 'Cargando...',
        none: '(ninguno)',
        notAvailable: 'No disponible',
        optional: 'opcional',
        terminate: '(terminar)',
        unknown: 'Desconocido'
      },
      boolean: { yes: 'Sí', no: 'No' },
      theme: {
        light: 'claro',
        dark: 'oscuro',
        toggle: 'Cambiar tema',
        switchTo: 'Cambiar al tema {{theme}}',
        switchMode: 'Cambiar al modo {{theme}}'
      }
    },
    navigation: {
      sections: {
        observability: 'Observabilidad',
        flows: 'Flujos',
        administration: 'Administración',
        system: 'Sistema'
      },
      setupWizard: 'Asistente de configuración',
      openSetupWizard: 'Abrir asistente de configuración',
      toggleSidebar: 'Mostrar u ocultar barra lateral',
      github: 'Ver Tempo en GitHub'
    },
    login: {
      subtitle: 'Orquestación de flujos de datos',
      serverUrl: 'URL del servidor',
      password: 'Contraseña',
      tenantId: 'ID del tenant',
      connecting: 'Conectando...',
      connectError: 'No se pudo conectar. Verifica la URL y las credenciales.',
      seededCredentials: 'Credenciales iniciales predeterminadas: admin@tempo.local / password'
    },
    components: {
      table: {
        pagination: {
          groupLabel: 'Paginación de tabla',
          records_one: '{{count}} registro',
          records_other: '{{count}} registros',
          perPage: 'Por página',
          page: 'Página',
          of: 'de',
          firstPage: 'Primera página',
          previousPage: 'Página anterior',
          nextPage: 'Página siguiente',
          lastPage: 'Última página'
        },
        selectAllRows: 'Seleccionar todas las filas',
        selectRow: 'Seleccionar fila',
        empty: 'No se encontraron elementos'
      },
      modal: {
        close: 'Cerrar'
      }
    }
  },
  'zh-Hans': {
    common: {
      language: { label: '语言', selector: '选择仪表板语言' },
      actions: {
        cancel: '取消',
        clear: '清除',
        close: '关闭',
        copy: '复制到剪贴板',
        copied: '已复制',
        create: '创建',
        delete: '删除',
        done: '完成',
        edit: '编辑',
        execute: '执行',
        refresh: '刷新',
        reset: '重置',
        save: '保存',
        saveAll: '全部保存',
        signIn: '登录',
        signOut: '退出登录',
        view: '查看',
        viewJson: '查看 JSON'
      },
      generic: {
        active: '活动',
        any: '任意',
        empty: '（空）',
        loading: '加载中...',
        none: '（无）',
        notAvailable: '不可用',
        optional: '可选'
      },
      boolean: { yes: '是', no: '否' },
      theme: {
        light: '浅色',
        dark: '深色',
        toggle: '切换主题',
        switchTo: '切换到{{theme}}主题',
        switchMode: '切换到{{theme}}模式'
      }
    },
    navigation: {
      sections: {
        observability: '可观测性',
        flows: '流程',
        administration: '管理',
        system: '系统'
      },
      setupWizard: '设置向导',
      openSetupWizard: '打开设置向导',
      toggleSidebar: '切换侧边栏',
      github: '在 GitHub 上查看 Tempo'
    },
    login: {
      subtitle: '数据流编排',
      serverUrl: '服务器地址',
      email: '电子邮箱',
      password: '密码',
      tenantId: '租户 ID',
      tenantPlaceholder: '管理员登录请留空',
      connecting: '连接中...',
      connectError: '连接失败。请检查 URL 和凭据。'
    }
  },
  'yue-Hant-HK': {
    common: {
      language: { label: '語言', selector: '選擇儀表板語言' },
      actions: {
        cancel: '取消',
        clear: '清除',
        close: '關閉',
        copy: '複製到剪貼簿',
        copied: '已複製',
        create: '建立',
        delete: '刪除',
        done: '完成',
        edit: '編輯',
        execute: '執行',
        refresh: '重新整理',
        reset: '重設',
        save: '儲存',
        saveAll: '全部儲存',
        signIn: '登入',
        signOut: '登出',
        view: '查看',
        viewJson: '查看 JSON'
      },
      generic: {
        active: '啟用',
        any: '任何',
        empty: '（空）',
        loading: '載入中...',
        none: '（無）',
        optional: '可選'
      },
      boolean: { yes: '是', no: '否' }
    },
    navigation: {
      sections: {
        observability: '可觀測性',
        flows: '流程',
        administration: '管理',
        system: '系統'
      },
      setupWizard: '設定精靈',
      openSetupWizard: '開啟設定精靈'
    },
    login: {
      subtitle: '資料流程編排',
      serverUrl: '伺服器 URL',
      email: '電郵',
      password: '密碼',
      tenantId: '租戶 ID',
      tenantPlaceholder: '管理員登入可留空',
      connecting: '連線中...',
      connectError: '連線失敗。請檢查 URL 同憑證。'
    }
  },
  ja: {
    common: {
      language: { label: '言語', selector: 'ダッシュボードの言語を選択' },
      actions: {
        cancel: 'キャンセル',
        clear: 'クリア',
        close: '閉じる',
        copy: 'クリップボードにコピー',
        copied: 'コピーしました',
        create: '作成',
        delete: '削除',
        done: '完了',
        edit: '編集',
        execute: '実行',
        refresh: '更新',
        reset: 'リセット',
        save: '保存',
        saveAll: 'すべて保存',
        signIn: 'サインイン',
        signOut: 'サインアウト',
        view: '表示',
        viewJson: 'JSON を表示'
      },
      generic: {
        active: '有効',
        any: 'すべて',
        empty: '（空）',
        loading: '読み込み中...',
        none: '（なし）',
        optional: '任意'
      },
      boolean: { yes: 'はい', no: 'いいえ' },
      theme: {
        light: 'ライト',
        dark: 'ダーク',
        toggle: 'テーマを切り替え',
        switchTo: '{{theme}} テーマに切り替え',
        switchMode: '{{theme}} モードに切り替え'
      }
    },
    navigation: {
      sections: {
        observability: '可観測性',
        flows: 'フロー',
        administration: '管理',
        system: 'システム'
      },
      setupWizard: 'セットアップウィザード',
      openSetupWizard: 'セットアップウィザードを開く'
    },
    login: {
      subtitle: 'データフローオーケストレーション',
      serverUrl: 'サーバー URL',
      email: 'メール',
      password: 'パスワード',
      tenantId: 'テナント ID',
      tenantPlaceholder: '管理者ログインの場合は空欄のままにしてください',
      connecting: '接続中...',
      connectError: '接続に失敗しました。URL と認証情報を確認してください。'
    }
  },
  de: {
    common: {
      language: { label: 'Sprache', selector: 'Dashboard-Sprache auswählen' },
      actions: {
        cancel: 'Abbrechen',
        clear: 'Leeren',
        close: 'Schließen',
        copy: 'In Zwischenablage kopieren',
        copied: 'Kopiert!',
        create: 'Erstellen',
        delete: 'Löschen',
        done: 'Fertig',
        edit: 'Bearbeiten',
        execute: 'Ausführen',
        refresh: 'Aktualisieren',
        reset: 'Zurücksetzen',
        save: 'Speichern',
        saveAll: 'Alles speichern',
        signIn: 'Anmelden',
        signOut: 'Abmelden',
        view: 'Anzeigen',
        viewJson: 'JSON anzeigen'
      },
      generic: {
        active: 'Aktiv',
        any: 'Beliebig',
        empty: '(leer)',
        loading: 'Wird geladen...',
        none: '(keine)',
        optional: 'optional'
      },
      boolean: { yes: 'Ja', no: 'Nein' }
    },
    navigation: {
      sections: {
        observability: 'Beobachtbarkeit',
        flows: 'Flows',
        administration: 'Verwaltung',
        system: 'System'
      }
    },
    login: {
      subtitle: 'Datenfluss-Orchestrierung',
      serverUrl: 'Server-URL',
      email: 'E-Mail',
      password: 'Passwort',
      tenantId: 'Mandanten-ID',
      tenantPlaceholder: 'Für die Administratoranmeldung leer lassen',
      connecting: 'Verbinden...',
      connectError: 'Verbindung fehlgeschlagen. Prüfen Sie URL und Zugangsdaten.'
    }
  },
  fr: {
    common: {
      language: { label: 'Langue', selector: 'Choisir la langue du tableau de bord' },
      actions: {
        cancel: 'Annuler',
        clear: 'Effacer',
        close: 'Fermer',
        copy: 'Copier dans le presse-papiers',
        copied: 'Copié !',
        create: 'Créer',
        delete: 'Supprimer',
        done: 'Terminé',
        edit: 'Modifier',
        execute: 'Exécuter',
        refresh: 'Actualiser',
        reset: 'Réinitialiser',
        save: 'Enregistrer',
        saveAll: 'Tout enregistrer',
        signIn: 'Se connecter',
        signOut: 'Se déconnecter',
        view: 'Voir',
        viewJson: 'Voir le JSON'
      },
      generic: {
        active: 'Actif',
        any: 'Tous',
        empty: '(vide)',
        loading: 'Chargement...',
        none: '(aucun)',
        optional: 'facultatif'
      },
      boolean: { yes: 'Oui', no: 'Non' }
    },
    login: {
      subtitle: 'Orchestration des flux de données',
      serverUrl: 'URL du serveur',
      email: 'E-mail',
      password: 'Mot de passe',
      tenantId: 'ID du locataire',
      tenantPlaceholder: 'Laisser vide pour une connexion administrateur',
      connecting: 'Connexion...',
      connectError: 'Échec de la connexion. Vérifiez l’URL et les identifiants.'
    }
  },
  it: {
    common: {
      language: { label: 'Lingua', selector: 'Scegli la lingua della dashboard' },
      actions: {
        cancel: 'Annulla',
        clear: 'Cancella',
        close: 'Chiudi',
        copy: 'Copia negli appunti',
        copied: 'Copiato!',
        create: 'Crea',
        delete: 'Elimina',
        done: 'Fine',
        edit: 'Modifica',
        execute: 'Esegui',
        refresh: 'Aggiorna',
        reset: 'Reimposta',
        save: 'Salva',
        saveAll: 'Salva tutto',
        signIn: 'Accedi',
        signOut: 'Disconnetti',
        view: 'Visualizza',
        viewJson: 'Visualizza JSON'
      },
      generic: {
        active: 'Attivo',
        any: 'Qualsiasi',
        empty: '(vuoto)',
        loading: 'Caricamento...',
        none: '(nessuno)',
        optional: 'facoltativo'
      },
      boolean: { yes: 'Sì', no: 'No' }
    },
    login: {
      subtitle: 'Orchestrazione dei flussi di dati',
      serverUrl: 'URL del server',
      email: 'Email',
      password: 'Password',
      tenantId: 'ID tenant',
      tenantPlaceholder: 'Lascia vuoto per l’accesso amministratore',
      connecting: 'Connessione...',
      connectError: 'Connessione non riuscita. Controlla URL e credenziali.'
    }
  },
  'zh-Hant-TW': {
    common: {
      language: { label: '語言', selector: '選擇儀表板語言' }
    }
  },
  ko: {
    common: {
      language: { label: '언어', selector: '대시보드 언어 선택' }
    }
  },
  'pt-BR': {
    common: {
      language: { label: 'Idioma', selector: 'Escolher idioma do painel' }
    }
  },
  ar: {
    common: {
      language: { label: 'اللغة', selector: 'اختر لغة لوحة التحكم' }
    }
  }
};

const localeQualityOverrides = {
  es: {
    common: {
      actions: {
        refresh: 'Actualizar',
        reload: 'Recargar',
        skipSetup: 'Omitir configuración'
      }
    },
    navigation: {
      items: {
        explorer: { label: 'Explorador de API' },
        artifacts: { label: 'Artefactos' },
        logs: { label: 'Registros' },
        workers: { label: 'Trabajadores' },
        settings: { label: 'Configuración' }
      }
    },
    views: {
      apiExplorer: {
        title: 'Explorador de API',
        resources: 'Recursos'
      },
      artifacts: { title: 'Artefactos' },
      logs: { title: 'Registros' },
      settings: { title: 'Configuración' },
      workers: { title: 'Trabajadores' }
    },
    'API Explorer': 'Explorador de API',
    'API auth': 'Autenticacion API',
    'Admin API key (bypass)': 'Clave API de administrador (omisión)',
    'ARTIFACTS': 'ARTEFACTOS',
    'Artifacts': 'Artefactos',
    'Auto-refresh': 'Actualización automática',
    'Blocked': 'Bloqueado',
    'Console logging': 'Registro en consola',
    'Drain worker': 'Drenar trabajador',
    'Enabled': 'Habilitado',
    'File': 'Archivo',
    'GLOBAL ADMIN': 'ADMINISTRADOR GLOBAL',
    'Global Admin': 'Administrador global',
    'Global admin': 'Administrador global',
    'Host': 'Anfitrión',
    'Identifier': 'Identificador',
    'Last heartbeat': 'Último latido',
    'Logs': 'Registros',
    'Max': 'Máx.',
    'Modified': 'Modificado',
    'Online': 'En línea',
    'Open the dedicated log viewer for this worker': 'Abrir el visor de registros dedicado para este trabajador',
    'Open the log viewer scoped to this worker': 'Abrir el visor de registros de este trabajador',
    'REST listener': 'Escucha REST',
    'RESOURCES': 'RECURSOS',
    'Re-read': 'Volver a leer',
    'Refresh': 'Actualizar',
    'Refresh files': 'Actualizar archivos',
    'Refresh run': 'Actualizar ejecución',
    'Resources': 'Recursos',
    'Run Logs': 'Registros de ejecución',
    'Settings': 'Configuración',
    'Size': 'Tamaño',
    'Skip setup': 'Omitir configuración',
    'Succeeded': 'Completado',
    'SUCCEEDED': 'COMPLETADO',
    'TENANT ADMIN': 'ADMINISTRADOR DEL TENANT',
    'Task timeout': 'Tiempo de espera de tarea',
    'Tenant Admin': 'Administrador del tenant',
    'Tenant admin': 'Administrador del tenant',
    'Viewer': 'Visor',
    'View logs': 'Ver registros',
    'Worker': 'Trabajador',
    'Workers': 'Trabajadores'
  }
};

function buildLocaleResources(locale) {
  return {
    translation: deepMerge(
      deepMerge(en, localeOverrides[locale] || {}),
      deepMerge(
        generatedLocaleResources[locale]?.translation || {},
        localeQualityOverrides[locale] || {}
      )
    )
  };
}

export const resources = {
  en: { translation: en },
  es: buildLocaleResources('es'),
  'zh-Hans': buildLocaleResources('zh-Hans'),
  'yue-Hant-HK': buildLocaleResources('yue-Hant-HK'),
  ja: buildLocaleResources('ja'),
  de: buildLocaleResources('de'),
  fr: buildLocaleResources('fr'),
  it: buildLocaleResources('it'),
  'zh-Hant-TW': buildLocaleResources('zh-Hant-TW')
};

const localeAuditOverrides = {
  es: {
    common: {
      actions: {
        refresh: 'Actualizar',
        reload: 'Recargar',
        skipSetup: 'Omitir configuracion'
      }
    },
    navigation: {
      items: {
        explorer: { label: 'Explorador de API' },
        artifacts: { label: 'Artefactos' },
        logs: { label: 'Registros' },
        workers: { label: 'Trabajadores' },
        settings: { label: 'Configuracion' }
      }
    },
    views: {
      apiExplorer: {
        title: 'Explorador de API',
        resources: 'Recursos',
        headerPlaceholder: 'Encabezado',
        headers: 'Encabezados',
        paramPlaceholder: 'parametro',
        responseHeaders: 'Encabezados'
      },
      home: {
        failed: 'Fallidos',
        requestFailureLegend: 'Fallidos (4xx-5xx)',
        tenantsQueued: 'Tenants en cola'
      },
      requestHistory: {
        filters: {
          tenantTip: 'Restringir a un tenant (solo administradores)',
          userTip: 'Restringir a un usuario (solo administradores)'
        }
      },
      roles: {
        columns: { identifier: 'Identificador' },
        jsonTitle: 'JSON del rol'
      },
      artifacts: { title: 'Artefactos' },
      logs: { title: 'Registros' },
      settings: { title: 'Configuracion' },
      tenants: {
        columns: { identifier: 'Identificador' },
        jsonTitle: 'JSON del tenant'
      },
      workers: { title: 'Trabajadores' }
    },
    'API Explorer': 'Explorador de API',
    'Admin API key (bypass)': 'Clave API de administrador (omision)',
    'ARTIFACTS': 'ARTEFACTOS',
    'Ambiguous': 'Ambiguo',
    'Artifact': 'Artefacto',
    'Artifact JSON': 'JSON del artefacto',
    'Artifact SHA-256': 'SHA-256 del artefacto',
    'Artifact referenced by artifact-backed runtimes': 'Artefacto referenciado por runtimes basados en artefactos',
    'Artifacts': 'Artefactos',
    'Assigned': 'Asignado',
    'Assigned worker and execution node kind': 'Trabajador asignado y tipo de nodo de ejecucion',
    'Attempt': 'Intento',
    'Auto-refresh': 'Actualizacion automatica',
    'Blocked': 'Bloqueado',
    'Config DTO': 'DTO de configuracion',
    'Console logging': 'Registro en consola',
    'Default Tenant': 'Tenant predeterminado',
    'Drain worker': 'Drenar trabajador',
    'Editable below': 'Editable abajo',
    'Echo HTTP trigger': 'Disparador HTTP de eco',
    'Echo response body': 'Cuerpo de respuesta de eco',
    'Echo run': 'Ejecucion de eco',
    'Echo trigger': 'Disparador de eco',
    'Email': 'Correo electronico',
    'Enabled': 'Habilitado',
    'Event timestamp': 'Marca de tiempo del evento',
    'Flow ID': 'ID del flujo',
    'Flow JSON': 'JSON del flujo',
    'File': 'Archivo',
    'GLOBAL ADMIN': 'ADMINISTRADOR GLOBAL',
    'Global Admin': 'Administrador global',
    'Global admin': 'Administrador global',
    'Headers JSON': 'JSON de encabezados',
    'Host': 'Anfitrion',
    'HTTP headers sent with each request': 'Encabezados HTTP enviados con cada solicitud',
    'Identifier': 'Identificador',
    'Input': 'Entrada',
    'Input schema': 'Esquema de entrada',
    'LabelPinned': 'Fijado por etiqueta',
    'Last heartbeat': 'Ultimo latido',
    'Lease': 'Concesion',
    'Lifecycle state for this run': 'Estado del ciclo de vida de esta ejecucion',
    'Logs': 'Registros',
    'Loose': 'Flexible',
    'Master toggle for request capture middleware': 'Interruptor maestro del middleware de captura de solicitudes',
    'Max': 'Max.',
    'Max bytes': 'Bytes maximos',
    'Max response body bytes': 'Bytes maximos del cuerpo de la respuesta',
    'Modified': 'Modificado',
    'Mirror log entries to stdout': 'Reflejar entradas del registro en stdout',
    'Native': 'Nativo',
    'Non-online': 'No en linea',
    'On': 'Activado',
    'Online': 'En linea',
    'Open the dedicated log viewer for this worker': 'Abrir el visor de registros dedicado para este trabajador',
    'Open the log viewer scoped to this worker': 'Abrir el visor de registros de este trabajador',
    'Path to the SQLite database file (relative to the working directory)': 'Ruta al archivo de base de datos SQLite (relativa al directorio de trabajo)',
    'Per-step timeout in milliseconds': 'Tiempo de espera por paso en milisegundos',
    'Queue worker enabled': 'Trabajador de cola habilitado',
    'Queued': 'En cola',
    'Raw JSON': 'JSON sin formato',
    'REST listener': 'Escucha REST',
    'RESOURCES': 'RECURSOS',
    'Re-read': 'Volver a leer',
    'Refresh': 'Actualizar',
    'Refresh files': 'Actualizar archivos',
    'Refresh run': 'Actualizar ejecucion',
    'Resolved': 'Resuelto',
    'Resources': 'Recursos',
    'Run Logs': 'Registros de ejecucion',
    'Runtime config JSON': 'JSON de configuracion del runtime',
    'Schema': 'Esquema',
    'Settings': 'Configuracion',
    'Size': 'Tamano',
    'Skip setup': 'Omitir configuracion',
    'Stable key referenced by flow transitions': 'Clave estable referenciada por las transiciones del flujo',
    'Stable key referenced by the flow transition graph': 'Clave estable referenciada por el grafo de transiciones del flujo',
    'Stale': 'Obsoleto',
    'Step JSON': 'JSON del paso',
    'Succeeded': 'Completado',
    'SUCCEEDED': 'COMPLETADO',
    'TENANT ADMIN': 'ADMINISTRADOR DEL TENANT',
    'Task timeout': 'Tiempo de espera de tarea',
    'Tenant active': 'Tenant activo',
    'Tenant Admin': 'Administrador del tenant',
    'Tenant admin': 'Administrador del tenant',
    'Tenant queued': 'Tenant en cola',
    'Timeout': 'Tiempo de espera',
    'Timeout (ms)': 'Tiempo de espera (ms)',
    'Toggle between light and dark UI': 'Alternar entre la interfaz clara y oscura',
    'Transitions (JSON)': 'Transiciones (JSON)',
    'Transitions JSON is invalid.': 'El JSON de transiciones no es valido.',
    'Trigger': 'Disparador',
    'Trigger JSON': 'JSON del disparador',
    'Triggers': 'Disparadores',
    'Viewer': 'Visor',
    'View logs': 'Ver registros',
    'Worker Heartbeat Recency': 'Recencia del latido del trabajador',
    'Worker JSON': 'JSON del trabajador',
    'Worker': 'Trabajador',
    'Workers': 'Trabajadores',
    'artifact-capable': 'compatible con artefactos'
  },
  de: {
    'API auth': 'API-Authentifizierung',
    'Auto-refresh': 'Automatische Aktualisierung',
    'Block': 'Sperren',
    'Drain': 'Drain-Modus',
    'Event severity': 'Ereignisschwere',
    'Execution node type that ran the workload': 'Typ des Ausfuehrungsknotens fuer diese Ausfuehrung',
    'Global Admin': 'Globaler Administrator',
    'Global admin': 'Globaler Administrator',
    'Http': 'HTTP',
    'LabelPinned': 'Label-fixiert',
    'LeastLoaded': 'Geringste Auslastung',
    'Logs': 'Protokolle',
    'Max bytes': 'Max. Bytes',
    'Max concurrency': 'Max. Parallelitaet',
    'Queued': 'In Warteschlange',
    'Raw JSON': 'Roh-JSON',
    'Retained bytes': 'Beibehaltene Bytes',
    'Server URL': 'Server-URL',
    'Timeout (ms)': 'Zeitlimit (ms)',
    'Trigger ID': 'Trigger-ID',
    'Trigger JSON': 'Trigger-JSON',
    'Unblock': 'Entsperren',
    'Worker Heartbeat Recency': 'Aktualitaet des Worker-Heartbeats'
  },
  fr: {
    views: {
      apiExplorer: {
        paramPlaceholder: 'parametre'
      }
    },
    'API auth': 'Authentification API',
    'Flow JSON': 'JSON du flux',
    'Http': 'HTTP',
    'Master toggle for request capture middleware': 'Commutateur principal du middleware de capture des requetes',
    'Source IP': 'IP source',
    'Stale': 'Obsolete'
  },
  it: {
    views: {
      roles: {
        jsonTitle: 'JSON del ruolo'
      }
    },
    'API auth': 'Autenticazione API',
    'Auto-refresh': 'Aggiornamento automatico',
    'Binary': 'Binario',
    'Console logging': 'Registrazione su console',
    'Deny': 'Nega',
    'Echo HTTP trigger': 'Trigger HTTP echo',
    'Echo run': 'Esecuzione echo',
    'Echo trigger': 'Trigger echo',
    'Echo trigger URL': 'URL del trigger echo',
    'LeastLoaded': 'Meno carico',
    'Logs': 'Log',
    'Loose': 'Flessibile',
    'Re-read': 'Rileggi',
    'Role JSON': 'JSON del ruolo',
    'Runtime JSON': 'JSON del runtime',
    'Stale': 'Obsoleto',
    'Trigger JSON': 'JSON del trigger'
  }
};

for (const [locale, override] of Object.entries(localeAuditOverrides)) {
  resources[locale] = {
    translation: deepMerge(resources[locale].translation, override)
  };
}
