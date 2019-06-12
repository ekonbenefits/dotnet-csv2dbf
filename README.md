# dotnet-csv2dbf
Given a dbf prototype, create a new one filled with data from csv

```
Usage: csv2dbf [options] <Input>

Arguments:
  Input                            CSV Files to convert

Options:
  -?|-h|--help                     Show help information
  -d|--dbf <DBF_TEMPLATE>          The DBF Template
  -i|--input-encoding <ENCODING>   Input Encoding [Default: Detect]
  -o|--output <DIRECTORY>          Output Directory [Default: Current directory]
  -f|--filename <OUTPUT_FILENAME>  Output Filename [Default: <INPUT>.dbf]
  -e|--output-encoding <ENCODING>  Output Encoding [Default: utf-8]
```



Additional Build Option:

Create a single self contained exe using dotnet-warp.
https://github.com/Hubert-Rybak/dotnet-warp
