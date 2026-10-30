# Kodiak Windows Setup (first release)

This folder builds `output /Kodiak-Setup.exe`, a single executable containing the
published, self-contained .NET 10 API, compiled Vue website, and configuration
wizard. The target does not need Node.js, npm, the .NET SDK, or the source code.

Supported target: Windows x64 with Windows PowerShell 5.1, IIS, and PostgreSQL.
Run setup as administrator. Initial scope is new installations and recovery of
incomplete installations, not in-place upgrades of completed installations.

## Build

Run from the repository root on the build machine (.NET 10 SDK and Node.js required):

```powershell
.\installer\Build-Installer.ps1
```

To include prerequisites for a machine that does not already have them:

```powershell
.\installer\Build-Installer.ps1 `
  -HostingBundlePath 'C:\Installers\dotnet-hosting-10.0-win.exe' `
  -PostgresInstallerPath 'C:\Installers\postgresql-windows-x64.exe'
```

Supply the official Microsoft .NET 10 Hosting Bundle and official EDB PostgreSQL
Windows x64 installer. The build and setup both verify their Authenticode
signatures; SHA-256 hashes are stored in the package and checked before use.
Neither prerequisite is downloaded or installed on the build machine.
The basic build excludes prerequisite installers; it works on a prepared machine.
IIS feature installation may still need Windows installation media or Windows
Update, even when vendor installers are bundled.

The API and the launcher each include their runtime. The Hosting Bundle is still
required to register the ASP.NET Core IIS module. Enable IIS before installing
the Hosting Bundle, or repair the bundle afterward.

The build runs frontend/backend tests unless `-SkipTests` is explicitly passed.
Each build gets an isolated folder beneath `artifacts`. It excludes development
configuration and uploaded data. Output is not signed: apply your organization's
code-signing certificate before distributing outside a test environment.

## Installation wizard

1. Choose a dedicated, empty local installation folder.
2. Check prerequisites. Setup first validates IIS and the hosting module. Existing
   ready components are reused without feature changes or an IIS restart. Only
   the missing web server/management module is enabled; the optional IIS console,
   default-document and static-content features are not required. A restart
   message identifies the specific required component that is pending. If PostgreSQL is missing, choose
   **Install PostgreSQL** to open the bundled official vendor installer. Its wizard
   asks for server settings and a PostgreSQL administrator password. Return to
   Kodiak and refresh detected servers afterward. Alternatively, leave automatic
   local PostgreSQL installation enabled: if no local installation is detected and
   the selected local port is not already serving a database, Kodiak installs it
   unattended using the database username, password and port from the wizard.
   The password is passed in an administrator-protected temporary option file,
   deleted after the prerequisite installer exits, rather than on its command line.
3. Select a detected local server or enter any remote hostname and port. Local
   discovery checks installation registry entries, PostgreSQL Windows services,
   their configured data-directory ports, and PostgreSQL's local port 5432
   handshake. When PostgreSQL is detected, install/download controls are hidden
   and automatic installation is disabled. It does not scan networks.
   Previously tested host/port pairs are remembered without passwords.
4. Enter database credentials and choose **Test and list databases**.
5. Choose a new database name, or uncheck **Create a new empty application database**
   and select an existing application database. Creating a new database requires
   CREATE DATABASE and CREATE ROLE rights. Setup generates a dedicated application
   login, grants it ownership of the new database, and saves only that login for
   runtime use. Existing-database mode uses the supplied credentials and requires
   migration/ownership permissions; prefer a dedicated application owner.
6. Choose **This PC only**, **Office network / IP address**, or **Public domain**.
   Office mode lists the server's local IPv4 addresses; public mode accepts a
   domain and an optional public IPv4 DNS target. Select the local binding IP,
   port and certificate. Use **Choose certificate** to select an installed
   certificate from Local Computer / Personal. Public mode requires HTTPS and a
   certificate matching the domain. Local/office HTTPS can create a self-signed
   certificate if left blank; the browser will need to trust it separately.
7. Set the initial superadmin username/password. Choose whether to seed countries
   and states/provinces. Required schema, timezones and bootstrap records are
   installed; no demo shipments or business partners are created.
8. Install. Setup tests both the website and an authenticated API route through
   IIS and creates an Open Kodiak desktop shortcut after successful checks.

The optional firewall checkbox creates a dedicated rule for the website port.
Office rules allow the local subnet on Domain/Private profiles; public rules
allow the selected port on all profiles. This-PC mode is bound only to loopback
and never opens a firewall rule. No PostgreSQL firewall port is opened.

For a public domain, point its DNS A record at the public IP and configure router
or cloud firewall forwarding to the server's selected HTTPS port. A router's
public IP is not added to the server's network interfaces or IIS binding. DNS,
NAT, CGNAT workarounds, certificate issuance/renewal and trust are configured
outside this installer. Local checks validate IIS, not end-to-end public access.

## Change access for an existing installation

Open the latest setup, select the existing Kodiak folder on Installation, then
configure Website and choose **Update existing website**. The IIS site name must
match the installed application. This adds the requested binding while retaining
existing Kodiak bindings, database, API/Vue files, and application pool. It does
not rerun migrations or bootstrap. Existing bindings on other websites are not
changed; a conflicting IP/port/host produces an error. Failed local health checks
remove only the newly added binding. Existing certificate changes should be
managed separately in IIS if the requested binding already uses another certificate.

## Installed layout

```text
<selected folder>/
  Api/                         API binaries and IIS web.config
    appsettings.Production.json protected runtime database/JWT settings
  Web/                         compiled Vue HTML/CSS/JavaScript/assets
  Data/
    Photos/                    user photos and company logos
    ContainerAttachments/      uploaded shipment documents
  setup.log                    progress messages, excluding credentials
  setup-state.json              recovery marker for incomplete installations
  installed.json                completed installation manifest
  Uninstall-Kodiak.ps1
```

The API serves `Web` and `/api` on the same IIS site; no Node.js server or
separate reverse proxy is needed. `/api` paths never fall back to the Vue HTML.
Only administrators, SYSTEM and the dedicated application-pool identity can read
the installation. The pool can write `Data`, but cannot change application
binaries or runtime configuration. Database passwords and JWT keys are protected
by Windows ACLs; they are not encrypted at rest. PostgreSQL administrator and
initial superadmin passwords are not saved in the installed configuration.

## Failures, recovery and uninstall

- A reboot requirement stops before application provisioning; rerun after reboot.
- Never delete a failed installation's configuration: it holds the generated
  application database login needed to resume setup.
- Rerun setup with the same destination and website name after an incomplete
  installation. Saved application credentials are reused. Existing completed
  installations and unrelated folders/IIS sites are not overwritten.
- If database provisioning succeeds but configuration cannot be saved (for
  example, disk failure), the new database/role may remain. No database is dropped
  automatically. A PostgreSQL administrator must recover or remove these manually.
- Successful migrations are not reversed on failure. Existing-database mode asks
  you to confirm that you have a backup before applying migrations.
- To stop serving the app, run `Uninstall-Kodiak.ps1` as administrator. It removes
  the matching IIS website/pool, its tracked firewall rules and desktop shortcut, preserving the database,
  application files, configuration, certificates and uploads.
- Back up PostgreSQL, `Data`, and protected runtime settings together.

Automatic database backups, completed-installation upgrades,
certificate renewal, Windows Apps registration and automatic
prerequisite downloads are outside this first release.

## Verification and release gate

```powershell
.\installer\tests\Test-SetupScripts.ps1
.\installer\tests\Test-IisPrerequisites.ps1
.\installer\tests\Test-WebsiteAccess.ps1
```

This parses scripts and tests validation without enabling IIS or installing
anything. Build output alone does not prove installation works on clean Windows.
Before shipping, exercise the complete wizard in a disposable Windows VM:

- prepared IIS/PostgreSQL machine and fresh machine without prerequisites;
- bundled vendor installers, reboot path and missing-prerequisite messages;
- local/remote PostgreSQL, invalid credentials/TLS and database-name conflicts;
- new DB, existing DB, seeded/unseeded geography, failed setup/retry;
- hostname/port/certificate conflicts and HTTPS browser trust;
- office IPv4 bindings, public DNS/NAT, scoped firewall rules and existing-site access updates;
- login, shipment creation, logo/file upload and document preview after install;
- uninstall preserves database and uploaded files and other IIS sites.
