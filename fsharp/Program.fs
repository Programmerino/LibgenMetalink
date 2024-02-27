module LibgenMetalink

open FSharp.Data
open System
open System.Text.RegularExpressions
open FSharpPlus
open System.Net.Http

type LibgenRS = HtmlProvider<Sample="./data/index.php", PreferOptionals=true>
type Metalink = XmlProvider<Schema="./data/metalink4.xsd", InferTypesFromValues=true>
type Download = HtmlProvider<Sample="./data/download.html", PreferOptionals=true>
type Rocks = HtmlProvider<Sample="./data/rocks.html", PreferOptionals=true>

let makeHash typ value =
    let hash = Metalink.Hash(``type`` = Some typ)
    hash.XElement.Value <- value
    hash

let canon =
    [
        ("sha1", "sha-1")
        ("sha256", "sha-256")
    ] |> Map.ofList 

let canonHash x = Map.tryFind (String.toLower x) canon |> Option.defaultValue (String.toLower x)

let makeHashes map =
    map
    |> Map.toArray
    |> Array.map(fun (typ, text) ->
        makeHash (canonHash typ) (String.replace " " "" text)
    )

let makeTorrent link =
    let singleFileMetaurl = Metalink.Metaurl(lang=None, priority=None, mediatype="torrent", name=None)
    singleFileMetaurl.XElement.Value <- link
    singleFileMetaurl

// let gateways =
//     (new HttpClient()).GetStringAsync(
//         "https://raw.githubusercontent.com/ipfs/public-gateway-checker/master/src/gateways.ts"
//     )
//     |> Async.AwaitTask
//     |> Async.RunSynchronously
//     |> Regex(@"(?<=')[^\'\n]+(?=')", RegexOptions.Compiled).Matches
//     |> Array.ofSeq
//     |> Array.map(fun x -> x.Value)

let gateways =
    (new HttpClient()).GetStringAsync(
        "https://raw.githubusercontent.com/ipfs/public-gateway-checker/main/gateways.json"
    )
    |> Async.AwaitTask
    |> Async.RunSynchronously
    |> JsonValue.Parse
    |> fun x -> x.AsArray()
    |> Array.map(fun x -> $"{x.AsString()}/ipfs/:hash")

let ipfsToLinks ipfs =
    gateways
    |> Array.map(String.replace ":hash" ipfs)

let makeUrl link =
    let url = Metalink.Url(None, None, None)
    url.XElement.Value <- link
    url

let makeLanguage lang =
    let language = Metalink.Language(None)
    language.XElement.Value <- lang
    language

let makeVersion x =
    let version = Metalink.Version(None)
    version.XElement.Value <- x
    version

let makeMetalink lang filename urls torrentUrl hashMap size timeAdded =
    let hashes = makeHashes hashMap
    let language = makeLanguage lang
    let torrent = makeTorrent torrentUrl
    let urls = urls |> Array.map(makeUrl)
    let version = makeVersion timeAdded
    let file = Metalink.File(lang=None, name=filename, copyrights = [||], descriptions=[||], hashes=hashes, identities=[||], languages=[|language|], logoes=[||], metaurls=[|torrent|], urls=urls, os=[||], pieces=[||], publishers=[||], signatures=[||], sizes=[|string size|], versions=[|version|])

    Metalink.Metalink(lang=None, files=[|file|], generators = [||], origins = [||], publisheds = [||], updateds = [||])

let getHashes (x: LibgenRS) =
    x.Tables.Table2.Rows
    |> Array.map(fun x -> lift2 tuple2 x.hashtype x.hash)
    |> Array.choose id
    |> Map.ofArray

let getUrlMD5 (x: string) =
    let md5 = System.Web.HttpUtility.ParseQueryString(Uri(x).Query).Get("md5")
    md5

let getLolDoc md5 = Download.Load($"http://library.lol/main/{md5}")

let lolDocDownloadLink (x: Download) = x.Html.CssSelect("#download > h2 > a").Head.AttributeValue "href"

let getIPFSHash (x: Download) =
    let ipfsUrl = x.Lists.GET.Html.CssSelect("a").Head.AttributeValue "href"
    Uri(ipfsUrl).Segments.[2]

let getRocksUrl md5 =
    let url = $"https://libgen.rocks/ads.php?md5={md5}"
    Rocks.Load(url).Tables.``DB Dumps``.Html.CssSelect("a").Head.AttributeValue "href"
    // $"https://libgen.rocks/{path}"

let getLanguage (x: LibgenRS) = x.Html.CssSelect("body > table > tr").[6].CssSelect("td").[1].DirectInnerText()

let getTitle (x: LibgenRS) = x.Html.CssSelect("body > table > tr > td > b > a").Head.DirectInnerText()

let sanitizeString x = Regex.Replace(x, @"[^\w\.@-]", "_")

let getTorrentUrl md5 = $"http://libgen.rs/book/index.php?md5={String.toLower md5}&oftorrent="

let sizeFromString x =
    let pattern = @"\((\d+) bytes\)";
    Regex.Match(x, pattern).Groups[1].Value |> int

let getSize (x: LibgenRS) = x.Html.CssSelect("body > table > tr").[10].CssSelect("td").[1].DirectInnerText() |> sizeFromString

let getExtension (x: LibgenRS) = x.Html.CssSelect("body > table > tr").[10].CssSelect("td").[3].DirectInnerText()

let getTimeAdded (x: LibgenRS) = x.Html.CssSelect("body > table > tr").[8].CssSelect("td").[1].DirectInnerText()

[<EntryPoint>]
let main argv =
    let url = argv[0]
    let md5 = getUrlMD5 url

    // LibgenRS
    let doc = LibgenRS.Load(url)
    let hashes = getHashes doc
    let language = getLanguage doc
    let title = getTitle doc
    let extension = getExtension doc
    let filename = sanitizeString $"{title}.{extension}"
    let size = getSize doc
    let timeAdded = getTimeAdded doc
    
    // Rocks
    let rocksUrl = getRocksUrl md5

    // Library.lol
    let loldoc = getLolDoc md5
    let ipfsHash = getIPFSHash loldoc
    let ipfsUrls = ipfsToLinks ipfsHash
    let lolUrl = lolDocDownloadLink loldoc

    let urls = ([|
        rocksUrl
        lolUrl
    |] ++ ipfsUrls)

    let torrentUrl = getTorrentUrl md5

    let metalink = makeMetalink language filename urls torrentUrl hashes size timeAdded
    let str = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n{metalink}"
    printfn "%s" str
    0
