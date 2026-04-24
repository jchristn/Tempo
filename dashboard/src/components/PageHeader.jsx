import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { translateLiteral } from '../utils/i18n';

function PageHeader({ title, subtitle, actions }) {
  const { t } = useTranslation();
  const resolvedTitle = typeof title === 'string' ? translateLiteral(t, title) : title;
  const resolvedSubtitle = typeof subtitle === 'string' ? translateLiteral(t, subtitle) : subtitle;

  useEffect(() => {
    if (typeof resolvedTitle === 'string' && resolvedTitle) {
      document.title = resolvedTitle + ' | ' + t('common.appName');
      return;
    }
    document.title = t('common.appName');
  }, [resolvedTitle, t]);

  return (
    <div className="page-header">
      <div>
        <div className="page-header-title">{resolvedTitle}</div>
        {resolvedSubtitle && <div className="page-header-subtitle">{resolvedSubtitle}</div>}
      </div>
      {actions && <div className="page-header-actions">{actions}</div>}
    </div>
  );
}

export default PageHeader;
