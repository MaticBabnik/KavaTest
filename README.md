# ☕ KavaTest

Local test runner for P2 (FRI BVS).

> [!CAUTION]
> KavaTest seems to be a little more strict and might not fully match the original test runner. Always test with the `online` option before submitting.

## Usage

```
  list                 Lists specs from the remote server
  dump <specName>      Dumps a spec from the remote server
  test <sourceFile>    Tests a file locally
  online <sourceFile>  Tests a file on the remote server
```

## Building

Run `dotnet publish -r win-x64 -c Release` (`linux-64` for Linux), then grab your executable from `bin/Release/net8.0/<platform>/publish` and put it somwhere in `PATH`.
