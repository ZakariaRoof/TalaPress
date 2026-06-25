# Implementation Plan - Dynamic HQ Logo in Email Templates

The objective is to replace the hardcoded Horilla logo (`horilla-logo.png`) in both static file-based email templates and database-stored email templates (`HorillaMailTemplate`) with the HQ company logo (`hq=True`) configured in the system, while ensuring that the static logo remains as a fallback.

## User Review Required

> [!IMPORTANT]
> The dynamic logo resolution depends on the presence of `hq_company`, `protocol`, and `host` variables in the template rendering context. 
> To support database templates (`HorillaMailTemplate`) which are rendered in isolation (without RequestContext), we will:
> 1. Inject a helper function `get_mail_template_context` to add these variables to contexts where templates are rendered via python views/signals.
> 2. Implement a Django data migration to automatically update any existing database templates to replace references of the Horilla logo with a dynamic template conditional block.

## Proposed Changes

---

### Shared Utilities

#### [MODIFY] [methods.py](file:///e:/Projects/SSH/kshrms-local-dev/base/methods.py)
- Create `get_mail_template_context(request=None, **kwargs)` helper that loads `hq_company = Company.objects.filter(hq=True).order_by("pk").first()`, detects `protocol` and `host` from either the provided request or the request context via thread locals, and merges them with other keyword arguments.

---

### Python Views & Signals

#### [MODIFY] [actions.py](file:///e:/Projects/SSH/kshrms-local-dev/recruitment/views/actions.py)
- Update `get_template`, `get_template_hint`, and `get_mail_preview` to build the template Context using `get_mail_template_context(request, ...)`.

#### [MODIFY] [views.py](file:///e:/Projects/SSH/kshrms-local-dev/recruitment/views/views.py)
- Update `send_acknowledgement` to build the template Context using `get_mail_template_context(request, ...)`.

#### [MODIFY] [views.py](file:///e:/Projects/SSH/kshrms-local-dev/onboarding/views.py)
- Update `email_send` to build the template Context using `get_mail_template_context(request, ...)`.

#### [MODIFY] [signals.py](file:///e:/Projects/SSH/kshrms-local-dev/horilla_automations/signals.py)
- Update `signals.py` to build the template contexts using `get_mail_template_context(request, ...)`.

#### [MODIFY] [not_in_out_dashboard.py](file:///e:/Projects/SSH/kshrms-local-dev/employee/not_in_out_dashboard.py)
- Update `get_mail_preview` and `send_mail_to_employee` to build the template Context using `get_mail_template_context(request, ...)`.

---

### Static HTML Email Templates

#### [MODIFY] [default.html](file:///e:/Projects/SSH/kshrms-local-dev/payroll/templates/payroll/mail_templates/default.html)
#### [MODIFY] [default.html](file:///e:/Projects/SSH/kshrms-local-dev/onboarding/templates/onboarding/mail_templates/default.html)
#### [MODIFY] [ticket_mail.html](file:///e:/Projects/SSH/kshrms-local-dev/helpdesk/templates/helpdesk/mail_templates/ticket_mail.html)
- Replace `<img src="{{protocol}}://{{host}}{% static '/images/ui/horilla-logo.png' %}" ... />` with a dynamic logo check block:
```html
{% if hq_company and hq_company.icon %}
  <img src="{{protocol}}://{{host}}{{hq_company.icon.url}}" height="30" width="110.4" />
{% else %}
  <img src="{{protocol}}://{{host}}{% static '/images/ui/horilla-logo.png' %}" height="30" width="110.4" />
{% endif %}
```

#### [MODIFY] [leave_request_template.html](file:///e:/Projects/SSH/kshrms-local-dev/base/templates/base/mail_templates/leave_request_template.html)
- Replace the logo `<img>` block to resolve the double slash and dynamically use the HQ logo first, fallback to the instance company logo, and finally fallback to the static Horilla logo.

---

### Database Migration

#### [NEW] [0003_update_mail_templates_logo.py](file:///e:/Projects/SSH/kshrms-local-dev/base/migrations/0003_update_mail_templates_logo.py)
- Create a data migration that updates all `HorillaMailTemplate` objects in the database.
- It will replace Zoho/static URL references to the Horilla logo in the template bodies with:
```html
{% if hq_company and hq_company.icon %}{{protocol}}://{{host}}{{hq_company.icon.url}}{% else %}{{protocol}}://{{host}}/static/images/ui/horilla-logo.png{% endif %}
```

## Verification Plan

### Automated Tests
- Run `python manage.py compilemessages` or standard tests to check if django starts successfully.
- Run database migrations using `python manage.py migrate`.

### Manual Verification
- Render email preview via Candidate Acknowledgement page to verify logo displays (either fallback or custom HQ logo).
