# Notice

Groundwork is under the [MIT License](LICENSE).

## Where it comes from

Groundwork is built from [Valheim World Editor](https://github.com/Shuiei/valheim-world-editor) by Thibault Bene,
under the MIT License, Copyright (c) 2026 Thibault Bene. The editor's 3D view, brushes, selection, copy and paste,
history, Help window, settings, language catalogues, plugin loading and Claude connection are his work. Groundwork
keeps that code and its copyright notice, and removes everything tied to Valheim: its saves, worlds, game data,
generator, mods and live game connection.

Groundwork holds no data from Valheim or any other game.

## Sample models

`samples/Meadow/models` holds a few models of Quaternius's Stylized Nature MegaKit (https://quaternius.com), CC0 1.0
(public domain): no credit is required, but it is given.

## Typeface

The editor's typeface is [Space Grotesk](https://github.com/floriankarsten/space-grotesk) by Florian Karsten, under the
SIL Open Font License 1.1 (`editor/Desktop/Assets/Fonts/OFL.txt`).

## Third-party code

Libraries Groundwork is built with, each under its own license: [Avalonia](https://github.com/AvaloniaUI/Avalonia)
(MIT), [Silk.NET](https://github.com/dotnet/Silk.NET) (MIT), [SkiaSharp](https://github.com/mono/SkiaSharp) (MIT,
through Avalonia), [Markdig](https://github.com/xoofx/markdig) (BSD 2-Clause) for the Help window,
[SharpGLTF](https://github.com/vpenades/SharpGLTF) (MIT) for glTF models, [LibTiff.Net](https://github.com/BitMiracle/libtiff.net) (BSD 3-Clause) for GeoTIFF elevation models, and the [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) (Apache License 2.0) with ASP.NET Core (MIT) for
Claude's connection.

## LibTiff.Net

Groundwork's packages include LibTiff.Net, under these terms:

```
LibTiff.Net
Copyright (c) 2008-2019, Bit Miracle
All rights reserved.

Redistribution and use in source and binary forms, with or without modification, are permitted provided that the
following conditions are met:

Redistributions of source code must retain the above copyright notice, this list of conditions and the following
disclaimer.

Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following
disclaimer in the documentation and/or other materials provided with the distribution.

Neither the name of the Bit Miracle nor the names of its contributors may be used to endorse or promote products
derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES,
INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL BIT MIRACLE BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
POSSIBILITY OF SUCH DAMAGE.

This software is based in part on the Independent JPEG Group's JPEG software (http://www.ijg.org/),
Copyright (C) 1991-1998, Thomas G. Lane.

This software is based in part on LibTIFF - TIFF Library and Utilities (http://www.remotesensing.org/libtiff/),
Copyright (c) 1988-1997 Sam Leffler, Copyright (c) 1991-1997 Silicon Graphics, Inc.

Portions Copyright (C) 2006, ComponentAce (http://www.componentace.com). All rights reserved.

Redistribution and use in source and binary forms, with or without modification, are permitted provided that the
following conditions are met:

Redistributions of source code must retain the above copyright notice, this list of conditions and the following
disclaimer.

Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following
disclaimer in the documentation and/or other materials provided with the distribution.

Neither the name of ComponentAce nor the names of its contributors may be used to endorse or promote products derived
from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES,
INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```
