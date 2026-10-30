# First installer build verification

Date: 28 October 2026.

- Self-contained API and single-file Windows x64 setup published successfully.
- Vue build succeeded; 40 frontend tests passed.
- Backend suite: 75 passed, 8 optional PostgreSQL tests skipped in the normal build.
- The new PostgreSQL setup lifecycle test was also run separately with a real
  server: database/role creation, migrations, superadmin creation and repeated
  initialization passed. Both the test database and its login were cleaned up.
- Seven PowerShell scripts parsed; 15 installer validation/discovery checks passed.
- Mocked IIS prerequisite tests verified that a ready installation makes no
  feature changes, prerequisite repairs or service restarts. Pending components,
  missing management tools and missing hosting registration were tested separately.
- HTTPS binding regression test checks exact site/port/host selection, SNI setup
  and certificate assignment. `Set-WebBinding` uses `BindingInformation` rather
  than the unsupported `Protocol` parameter; `Get-WebBinding` retains its valid
  protocol filter.
- Access-mode tests cover loopback-only access, office IPv4 bindings, public
  domain/HTTPS validation, public-IP vs local-IP separation, firewall scope,
  existing-site binding conflict protection, and rollback of failed access updates.
- IP-based HTTPS tests verify certificate assignment without SNI. The wizard
  smoke test exercises all three access-mode defaults and the generated URLs.
- Existing PostgreSQL detection was verified against the local
  `postgresql-x64-12` Windows service. The wizard smoke test confirmed automatic
  PostgreSQL installation is disabled when an installation is detected.
- The complete wizard constructed its controls successfully in smoke-test mode.
- Published helper process preserved Unicode and special characters over stdin.
- Static-hosting tests verified Vue pages, assets and SPA navigation while
  keeping unknown API routes and missing assets as 404 responses.
- All 376 payload file hashes matched the generated manifest.
- Official Microsoft Hosting Bundle 10.0.14 and EDB PostgreSQL 18.6-5 installers
  were included and their trusted publisher signatures checked. The Hosting
  Bundle also matched Microsoft's published SHA-512 digest.
- Development configuration, application data and configured development
  signing/admin secrets were excluded from the payload.

Not yet verified: actual IIS feature installation, Hosting Bundle installation,
unattended PostgreSQL installation, reboot recovery, certificate binding, full
installation/uninstall, and the final executable's launch on a clean Windows VM.
The setup executable is unsigned and is a first test build, not a certified
production installer. Use the clean-machine checklist in README.md before release.
