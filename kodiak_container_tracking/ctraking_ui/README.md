# Kodiak Container Tracking UI

Vue 3 + Vite application. Reuses the original prototype fonts, slate-blue/cyan dark theme, blue light theme, and theme toggle. Authentication uses the real Web API; no demo credentials or simulated sign-in.

## Run locally

Start the backend in a separate terminal:

~~~powershell
cd E:\Personal\PL\Dev\Kodiak\kodiak_container_tracking\ctrackin_webapi
dotnet run --project src/Kodiak.ContainerTracking.Api --launch-profile https
~~~

Then start this UI:

~~~powershell
cd E:\Personal\PL\Dev\Kodiak\kodiak_container_tracking\ctraking_ui
npm install
npm run dev
~~~

Open http://127.0.0.1:5174 and sign in with your configured superadmin credentials.

## Configuration

Copy .env.example to .env.local if the backend URL differs. API_PROXY_TARGET defaults to https://localhost:7225; VITE_API_BASE_URL defaults to /api. Restart Vite after environment changes. The local Vite proxy bypasses local development certificate verification. It avoids needing cross-origin browser requests.

## Authentication

POST /api/auth/login sends userName/password. Access tokens and user details stay in memory. The refresh token is saved in sessionStorage for the current tab, rotated through POST /api/auth/refresh on page reload and shortly before expiry, and removed after successful logout or server rejection. Startup waits for server validation before showing the workspace. Refreshing the tab retains sign-in; closing it ends the saved tab session. Temporary connection failures retain the refresh token for a later reload. Only the theme preference is saved in localStorage. Failed logout retains the session for retry. Passwords and access tokens are never written to browser storage.

## Validate and build

~~~powershell
npm test
npm run build
~~~

Deploy dist through an HTTPS web server with /api reverse-proxied to the Web API. Vite's proxy is only for local development/preview. The signed-in page is an account confirmation shell; business screens are the next increment.

## Navigation and employees

The signed-in shell matches the prototype sidebar/header and has independently collapsible Workspace, Management, and Security sections. Administrators can open Management > Employees for API-backed paging, detail view, creation, editing, and deletion. Designation/organization options are loaded from all master-data pages. Updates and deletions use the fetched version in If-Match; conflicts require reopening the record.

Delete is a lifecycle action: DELETE /api/employees/{id} marks the employee Terminated and retains the profile, linked accounts and audit history. Editing status back to Active restores eligibility. The final active administrator is protected by the server. Restart the rebuilt backend before testing this new DELETE route. No database migration is required.

## Employee photographs

Employee Add/Edit has Browse photo, Edit / crop, and Remove actions. Position and zoom the square crop, select Use cropped photo, then Save employee. The saved crop is a 512 x 512 JPEG. Existing photos load through authenticated API requests rather than a public folder. Photo files are saved to the backend PhotoStorage:RootPath; the database stores only the relative path. Complete or cancel an active crop before saving.

Technical superadmin employees/accounts are hidden from other users by server-side access checks, not client-side filtering. The UI also omits the SUPERADMIN designation from non-superadmin selections.

## Shipper registration

On the login page, select Register as a shipper. Enter company/contact details, optionally choose one active consignee, and set a username/password (12-128 characters with confirmation). Country and business category are optional. Consignees can be searched by company name or generated code; the dropdown only contains active records. Registration can complete without a consignee, including when no consignee options are available.

Registration creates the shipper and linked Shipper application user together. On success the sign-in page shows the generated shipper code and pre-fills the username. Passwords are cleared and never persisted in browser storage. A registered shipper can sign in immediately, without administrator privileges. Restart the rebuilt backend before testing these new public registration routes.

## Business categories

Administrators can open Management > Business categories to list/page, view, add, edit, deactivate (Delete), and reactivate categories using the standard form popup. Code is required, unique, up to 50 letters/numbers/underscores/hyphens and saved uppercase; name is required up to 300 characters. Status controls availability for new shipper selections. Updates/deactivation send the fresh version in If-Match; concurrent changes require reopening the record. Deletion preserves existing shipper links. Restart the rebuilt API to enable the DELETE route; no migration is required.

## Consignee management

Management > Consignees provides paged list, detail view, Add/Edit popups, and Delete confirmation using the existing /api/consignees API. Forms include company name, contact person (stored as lastName by the current contract), email, phone, address, optional country and Active/Blacklisted status. Country choices load all pages of country master records. Generated consignee codes are read-only. Only administrators can access this screen and its API.

Delete marks the consignee Blacklisted, excludes it from new shipper/registration selections, and denies sign-in to linked consignee accounts. Existing shipper links and audit history are retained. Editing the status to Active reactivates it. Updates and delete include If-Match from the latest record version. Restart the rebuilt API before testing deletion; no database migration is required.

The registration popup now uses one optional searchable consignee combobox. Search by company name/code and select a matching result in the same control; the clear button removes the selection. Loading/error/empty states and keyboard navigation are included, stale search requests are discarded, and consignee lookup loads independently of country/category options. Typing alone does not select a consignee; the helper text shows whether a result is selected.

## Shipper management

Admins can open Management > Shippers to view all companies and add/edit/delete records. Shipper users see Management > My shipper details and can view/edit only their own company. The API enforces ownership; hiding buttons is not the access control. Status changes and company deletion are administrator-only. Delete marks a shipper Blacklisted and prevents its linked accounts from signing in; administrators can reactivate it.

The detail/edit popup includes contact/address details, optional country/category/consignee, and a Shipper logo editor with browse, crop, replace and remove actions. Files use the backend's configured PhotoStorage directory/share and only the relative path is saved in the database. No migration is needed; restart the rebuilt API before testing the new routes.

## Country, state/province and shipping port screens

Management includes Countries, States / Provinces and Shipping ports for administrators. Each has a paged list, view/add/edit popup, status and Delete (deactivate) confirmation. State records require a country. Port records include country, optional country-filtered state/province, city and time zone. State countries cannot be changed after creation; existing references survive deactivation. All mutations use the latest record version. Shipping ports need the backend ShippingPortMaster migration on each deployment.

## Container registration

Workspace > Containers provides the prototype registration popup, paged/searchable directory and view/edit actions. Administrators manage all registrations and can cancel; shippers register/edit their own; consignees have read access to registrations linked to them. Configure at least two active shipping ports in Management before registering.

The popup accepts multiple JPEG/PNG container photos and PDF/DOCX/XLSX/TXT reference documents, previews/downloads saved attachments, and supports removal. The default API limits are 10 MiB per file and 20 files per registration. If an upload fails after saving, retry in the same popup to continue uploading against the existing registration. Storage is configured through ContainerStorage in the API appsettings.json; browser code does not receive storage paths.

Container directory toolbar: live search (300 ms debounce) covers booking/container, shipper, reference and origin/destination port names/codes. All statuses / Booked / Cancelled filters run on the API and reset pagination. Export downloads a UTF-8 CSV of all matching authorized rows across pages, using the search/status selected when Export was clicked. Spreadsheet formula cells are escaped. Later tracking stages are not part of the current registration status model.
