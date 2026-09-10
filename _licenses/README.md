# 📜 Licenses

This folder contains licenses for third-party libraries incorporated in the project. It serves to document the legal usage rights and obligations associated with these external components to ensure compliance and transparency throughout the development lifecycle.

## 🔍 Audit Result

The project does not bundle or vendor any third-party library. The website (`docs/`) is plain HTML, CSS and JavaScript written for this project: no CDN scripts, no web fonts, no frameworks. The solver (`solver/`) has no NuGet package references. The only external code is the platform each part runs on.

## 📦 External Components

|Component|Used by|License|File|
|----|-----|-----|-----|
| .NET 10 runtime & base class library (`System.*`, `Microsoft.NETCore.App`) | `solver/*.cs`, `solver/solver.csproj` | MIT, © .NET Foundation and Contributors | [dotnet-runtime_MIT.txt](dotnet-runtime_MIT.txt) |
| .NET binaries as distributed by Microsoft for Windows (SDK / runtime installer) | building and running `solver/` on Windows | Microsoft Software License Terms – Microsoft .NET Library | [dotnet-windows-binaries_LICENSE.txt](dotnet-windows-binaries_LICENSE.txt) |
| Python 3 standard library (`os`, `re`, `random`, `statistics`, `collections`) | `solver/repeat_objective_test.py` | PSF License Agreement (Python 3.13) | [python-3.13_PSF-LICENSE.txt](python-3.13_PSF-LICENSE.txt) |

Neither the .NET runtime nor the Python interpreter is shipped with this repository — both are installed separately by whoever builds or runs the code. The solver is built framework-dependent, so no .NET binaries end up in the output either. If a self-contained .NET build is ever published, also ship `ThirdPartyNotices.txt` from the .NET installation directory alongside it.

## 📖 Third-Party Content (not code)

|Content|Used in|Source|
|----|-----|-----|
| Text of *Codes and Ciphers* by Alexander d'Agapeyeff (Oxford University Press, London 1939; Gale Research facsimile, Detroit 1974) | `solver/corpus.txt` (full OCR text, used as language-model corpus), the cryptogram in `docs/assets/data.js`, quotations throughout `docs/` | Scan of the Elgin Community College copy, digitized by the Internet Archive with funding from the Kahle/Austin Foundation |

This is a book, not software, and carries no open-source license. The copyright status of the 1939 text depends on jurisdiction and should be checked before `corpus.txt` is redistributed any further than it already is.

🐟 Bugfish
