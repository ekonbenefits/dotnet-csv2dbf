
open System
open McMaster.Extensions.CommandLineUtils
open System.IO
open FSharp.Interop.Compose.IO
open FSharp.Interop.Compose.System
open FSharp.Interop.NullOptAble
open FSharp.Interop.NullOptAble.Operators
open FSharp.Data
open DotNetDBF
open UtfUnknown

let processCSV (input:string) (template:string) (outDir:string option) (outEncoding:string option) (outFilename:string option) (inEncoding: string option) =
    let csv = input |> Path.GetFullPath
    let dbf = template |> Path.GetFullPath
    let dir = outDir |?-> lazy (Environment.CurrentDirectory |> Path.GetFullPath)
    let outEnc = outEncoding |?-> lazy "utf-8"
    let filename = outFilename |?-> lazy (csv |> Path.getFileName |> Path.changeExtension "dbf")

    let getInputEncoding () =
        use fs = File.OpenRead(csv)
        let result = CharsetDetector.DetectFromStream(fs)
        try
           
            printfn "Detecting encoding of '%s'. Found '%s'." csv result.Detected.EncodingName
            if result.Detected.Encoding |> isNull then
                raise <| Exception("Couldn't load encoding")
            else
                result.Detected.Encoding
        with exn -> 
            printfn "%s" exn.Message
            for ei in System.Text.Encoding.GetEncodings() do
                   let e = ei.GetEncoding();
                   Console.Write( "{0,-6} {1,-25} ", ei.CodePage, ei.Name )
                   Console.Write( "{0,-8} {1,-8} ", e.IsBrowserDisplay, e.IsBrowserSave )
                   Console.Write( "{0,-8} {1,-8} ", e.IsMailNewsDisplay, e.IsMailNewsSave )
                   Console.WriteLine( "{0,-8} {1,-8} ", e.IsSingleByte, e.IsReadOnly )

            printfn "Failed to detect charset. using 'utf-8'."
            System.Text.Encoding.UTF8
       
    let inEnc = match inEncoding with
                | Some(x) -> System.Text.Encoding.GetEncoding(x)
                | None -> getInputEncoding ()

    let data = CsvFile.Load(csv, hasHeaders=true, encoding = inEnc)

    let fields = 
        use fileStream = File.OpenRead(dbf)
        use dbfReader = new DBFReader(fileStream)
        dbfReader.Fields

    let tmpPath = [|dir; sprintf "%s.%s" filename "tmp"|] |> Path.Combine
    let finalPath = [|dir; filename|] |> Path.Combine
    do
        use outStream = File.Create(tmpPath)
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
                                    let clen = min c.Length f.FieldLength
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
    printfn "Writing or overwriting '%s'" finalPath;
    File.Copy(tmpPath, finalPath, overwrite=true)
    File.Delete(tmpPath)

let OptionToOption (opt:CommandOption<'T>) =
    if opt.HasValue() then
        opt.ParsedValue |> Some
    else
        None

[<EntryPoint>]
let main argv =
    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance)
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