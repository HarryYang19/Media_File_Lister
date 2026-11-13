# Third-party notices

This application bundles unmodified third-party command-line tools in its Windows release ZIP. These tools remain separate programs and retain their own licenses and copyrights.

## ExifTool

- Author: Phil Harvey
- Website: <https://exiftool.org/>
- Source: <https://github.com/exiftool/exiftool>
- Windows executable distribution: <https://sourceforge.net/projects/exiftool/files/> (official project download mirror)
- Version used by the build workflow: 13.63 (Windows x64 archive SHA-256: `7a4bff4b1d6ee15adef882837847f76c0ed23228d294feed285939b6c9b293bd`)
- License: dual-licensed under the same terms as Perl (GNU GPL or Perl Artistic License). This distribution includes the Perl Artistic License text in `LICENSES/Artistic-1.0-Perl.txt` and elects to rely on that license for the bundled ExifTool distribution, subject to its terms.

## MediaInfo CLI

- Copyright: Copyright (c) 2002-2025 MediaArea.net SARL
- Website: <https://mediaarea.net/MediaInfo>
- Source: <https://github.com/MediaArea/MediaInfo>
- Windows CLI distribution: <https://mediaarea.net/en/MediaInfo/Download/Windows>
- Version used by the build workflow: 26.10 (Windows x64 CLI archive SHA-256: `4cc94bd272903914c04f4f9a9f53a6c94b5ea6c6454cf738d29fa182c41e8a29`)
- License: BSD 2-Clause. The license text is included in `LICENSES/MediaInfo-BSD-2-Clause.txt`.

The build workflow downloads the official upstream Windows archives and does not modify the third-party executables. Their respective copyrights, notices, and license terms continue to apply. See the upstream websites and source repositories for complete license and source information.

## .NET Runtime

- Copyright: .NET Foundation and Contributors
- Source: <https://github.com/dotnet/runtime>
- License: MIT. The Windows application is published self-contained and includes the .NET runtime. The runtime MIT license and third-party notices are included in the release ZIP under `LICENSES/`.
