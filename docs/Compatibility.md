# Compatibility

| Input | Pin | Evidence |
| --- | --- | --- |
| KSA reference and registered release | Windows x64 `2026.11.25.5949+64c3726bd24373931e550030e4ba49c83fb46ed8` | L-01, local PE product metadata. Disposable and reference KSA.dll SHA-256 `fa741419e287fb7f673fba3212be622b5fc27db91fa4a6632828fb9da7b15c7e`. |
| .NET SDK | Windows x64 `10.0.100` | D-NET, official release metadata and verified ZIP SHA-512 `24b033418a3969effd49b4651ef7ebbffeb284773b99545d78dce61a82e57f38db7facdb013c609ba15573c072f0e093363ae470824a6847f3c6111078c1fb64`. |
| .NET launcher runtime | Windows x64 `10.0.10` | D-NET, official release metadata and verified ZIP SHA-512 `2161dfa1cf027cdc074de7195b5f206b17ebd829ae415b9e7c9ee5f06d3952b6583030022dbe0d6e9221b5c577c411d7cd5322241f6d2299d9c886641215699b`. |
| StarMap loader and API | `0.5.4+477f495d1c8d59910ad7f37071b77535f59f8c5d` | D-STAR / M-02, local PE metadata and upstream release workflow at the pinned revision. Release ZIP SHA-256 `bcf14ac68dfb6558152586fd98776fc5013006a0406c37792f5f6afcf0561366`. |
| Harmony | `2.4.3.0+b79f2f1ac971e5bc224cda3efedbbb4084efbfb6` | M-04 / D-STAR, local PE metadata for the loader's 0Harmony.dll and upstream central package props. |

The [StarMap release](https://github.com/StarMapLoader/StarMap/releases/tag/0.5.4) supplies the compile-time API and independently installed runtime loader. The release source declares MIT in `LICENSE.txt`. Its central KSA package is `.5801`, so callback behavior on `.5949` is tracked separately in [Verification](Verification.md). The mod uses local `.5949` game references. Build inputs stay outside the staged mod payload.
