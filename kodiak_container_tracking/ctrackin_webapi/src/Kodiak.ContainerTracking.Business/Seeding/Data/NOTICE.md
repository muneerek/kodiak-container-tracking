# Geography seed data

Snapshot retrieved 2026-10-20 from pycountry's packaged ISO data:

- Countries/territories: https://raw.githubusercontent.com/pycountry/pycountry/main/src/pycountry/databases/iso3166-1.json
- Subdivisions: https://raw.githubusercontent.com/pycountry/pycountry/main/src/pycountry/databases/iso3166-2.json
- Project/license: https://github.com/pycountry/pycountry (LICENSE-pycountry.txt included)

The committed snapshot contains 249 ISO alpha-2 country/territory entries and 77 subdivisions: India 36, United Arab Emirates 7, China 34. This is a location-code catalogue following the source dataset, not an application assertion about political recognition or territorial sovereignty. Country names prefer the source common_name when available. Subdivision labels use source names with diacritics removed; UAE emirates use conventional English display names. ISO codes are unchanged. Additional country/territory entries in ISO 3166-1 are preserved independently of the ISO 3166-2 subdivision list.

No runtime network download is required. Update the committed snapshot deliberately when geography data changes. Stable codes serve as seed identity; customer-edited names and inactive flags are not overwritten.
