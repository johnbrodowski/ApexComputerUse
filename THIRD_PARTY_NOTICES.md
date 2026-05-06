# Third-Party Notices

ApexComputerUse depends on the following third-party packages. They are
distributed under their own licenses, reproduced or referenced below.
ApexComputerUse itself is licensed separately (see [LICENSE](LICENSE)).

---

## FlaUI.Core, FlaUI.UIA3

- Project: https://github.com/FlaUI/FlaUI
- License: MIT

```
Copyright (c) 2016 Roemer

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## Serilog, Serilog.Sinks.File

- Project: https://github.com/serilog/serilog
- License: Apache License 2.0
- Full text: https://www.apache.org/licenses/LICENSE-2.0

```
Copyright Serilog Contributors

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
```

---

## LLamaSharp, LLamaSharp.Backend.Cpu

- Project: https://github.com/SciSharp/LLamaSharp
- License: MIT

```
MIT License

Copyright (c) 2023 Yaohui Liu, Haiping Chen

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND.
```

---

## Telegram.Bot

- Project: https://github.com/TelegramBots/Telegram.Bot
- License: MIT

```
MIT License

Copyright (c) 2016 Robin Müller and contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND.
```

---

## Tesseract (.NET wrapper)

- Project: https://github.com/charlesw/tesseract
- License: Apache License 2.0
- Full text: https://www.apache.org/licenses/LICENSE-2.0

```
Copyright 2012-2019 Charles Weld

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0
```

The underlying Tesseract OCR engine (https://github.com/tesseract-ocr/tesseract)
is licensed under Apache License 2.0.

---

## System.ServiceProcess.ServiceController

- Project: https://github.com/dotnet/runtime
- License: MIT
- Copyright (c) .NET Foundation and Contributors

---

## xUnit, xunit.runner.visualstudio

- Project: https://github.com/xunit/xunit
- License: Apache License 2.0
- Copyright (c) .NET Foundation and Contributors
- Full text: https://www.apache.org/licenses/LICENSE-2.0

*(Test-only dependency; not distributed with the application.)*

---

## Microsoft.NET.Test.Sdk

- Project: https://github.com/microsoft/vstest
- License: MIT
- Copyright (c) .NET Foundation and Contributors

*(Test-only dependency; not distributed with the application.)*

---

## coverlet.collector

- Project: https://github.com/coverlet-coverage/coverlet
- License: MIT
- Copyright (c) 2018 Toni Solarin-Sodara

*(Test-only dependency; not distributed with the application.)*

---

## Notes

- License texts above are reproduced in good faith from the upstream projects
  at the package versions listed in the `.csproj` files. The authoritative
  license is the one shipped inside each NuGet package.
- ApexComputerUse does not modify the source of any of these dependencies; they
  are consumed as published NuGet packages.
- Test-only dependencies (xUnit, Microsoft.NET.Test.Sdk, coverlet) are used
  only to build and run the test suite and are not distributed with the
  application binary.
