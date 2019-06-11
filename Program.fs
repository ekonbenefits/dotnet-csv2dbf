// Learn more about F# at http://fsharp.org

open System
open McMaster.Extensions.CommandLineUtils
open System.IO
open FSharp.Interop.Compose.IO
open FSharp.Interop.Compose.System
open FSharp.Interop.NullOptAble
open FSharp.Interop.NullOptAble.Operators
open FSharp.Data
open DotNetDBF

let processCSV (input:string) (template:string) (outDir:string option) (outEncoding:string option) (outFilename:string option) (inEncoding: string option) =
    let csv = input |> Path.GetFullPath
    let dbf = template |> Path.GetFullPath
    let dir = outDir |?-> lazy (Environment.CurrentDirectory |> Path.GetFullPath)
    let outEnc = outEncoding |?-> lazy "utf-8"
    let filename = outFilename |?-> lazy (csv |> Path.getFileName |> Path.changeExtension "dbf")

    let getInputEncoding () =
        use fs = File.OpenRead(csv)
        let cdet = Ude.CharsetDetector()
        cdet.Feed(fs)
        cdet.DataEnd()
        try
            System.Text.Encoding.GetEncoding(cdet.Charset) |> ignore
            cdet.Charset
        with _ -> 
            "utf-8"
       
    let inEnc = inEncoding |?-> Lazy<string> getInputEncoding

    let data = CsvFile.Load(csv, hasHeaders=true, encoding = System.Text.Encoding.GetEncoding(inEnc))

    let fields = 
        use fileStream = File.OpenRead(dbf)
        use dbfReader = new DBFReader(fileStream)
        dbfReader.Fields

    let finalPath = [|dir; filename|] |> Path.Combine
    use outStream = File.Create(finalPath)
    use dbfWriter = new DBFWriter(outStream)
    dbfWriter.CharEncoding <- System.Text.Encoding.GetEncoding(outEnc)
    dbfWriter.Fields <- fields

    let csvColumns  = data.NumberOfColumns;

    for row in data.Rows do
        let record = ResizeArray()
        for (i,f) in Seq.indexed fields do
                let cell:obj =
                    if i < csvColumns then
                        match f.DataType with
                            | NativeDbType.Date -> 
                                seq {
                                    yield DateTime.TryParseExact(row.[i],"MM/dd/yyyy", null, Globalization.DateTimeStyles.None) |> Option.ofTryTuple
                                    yield DateTime.TryParseExact(row.[i],"yyyy-MM-dd", null, Globalization.DateTimeStyles.None) |> Option.ofTryTuple
                                    yield DateTime.TryParse(row.[i]) |> Option.ofTryTuple
                                } |> Seq.choose id |> Seq.tryHead |> Option.toNullable |> box
                            | NativeDbType.Numeric ->
                                Decimal.TryParse(row.[i]) |> Option.ofTryTuple |> Option.toNullable |> box
                            | NativeDbType.Char ->
                                let c = row.[i]
                                let clen = min c.Length f.Size
                                c |> String.Full.substring 0 clen |> box
                            | NativeDbType.Memo ->
                                MemoValue(row.[i]) |> box
                            | NativeDbType.Logical ->
                                let l = row.[i]
                                (l.StartsWith("T") || l.StartsWith("Y") || l.StartsWith("1")) |> box
                            | _ -> raise <| InvalidDataException("Only Support Clipper/DBF III data fields")
                    else null
                record.Add(cell)
        dbfWriter.WriteRecord(record.ToArray())


    

    ()

let OptionToOption (opt:CommandOption<'T>) =
    if opt.HasValue() then
        opt.ParsedValue |> Some
    else
        None

[<EntryPoint>]
let main argv =
    use app = new CommandLineApplication();
    app.HelpOption() |> ignore;
    let reqTemplate = app.Option<string>("-d|--dbf <DBF_TEMPLATE>", "The DBF Template", CommandOptionType.SingleValue).IsRequired()
    let optInputEncoding = app.Option<string>("-i|--input-encoding <ENCODING>", "Input Encoding", CommandOptionType.SingleValue) 
    let optOutDir = app.Option<string>("-o|--output <DIRECTORY>", "Output Directory", CommandOptionType.SingleValue)
    let optFileName = app.Option<string>("-f|--filename <OUTPUT_FILENAME>", "Output Filename", CommandOptionType.SingleValue)
    let optOutputEncoding = app.Option<string>("-e|--output-encoding <ENCODING>", "Output Encoding [Default: utf-8]", CommandOptionType.SingleValue)
    let input = app.Argument<string>("Input", "CSV Files to convert", multipleValues=true).IsRequired();

    app.OnExecute(Action(
                    fun ()->
                        for i in input.Values do
                            processCSV i (reqTemplate.ParsedValue) (optOutDir |> OptionToOption) (optOutputEncoding |> OptionToOption) (optFileName |> OptionToOption) (optInputEncoding |> OptionToOption)
                  ))

    app.Execute(argv)



