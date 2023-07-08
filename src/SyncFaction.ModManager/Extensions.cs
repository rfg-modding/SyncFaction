using System.Text;
using System.Xml;

namespace SyncFaction.ModManager;

public static class Extensions
{
    /// <summary>
    /// Writes xmldoc without declaration to a memory stream. Stream is kept open and rewound to begin
    /// </summary>
    public static void SerializeToMemoryStream(this XmlDocument document, MemoryStream ms)
    {
        using (var tw = XmlWriter.Create(ms,
                   new XmlWriterSettings
                   {
                       CloseOutput = false,
                       //Indent = true, // NOTE: some files cant be reformatted or even minimized, game crashes if you do that
                       Encoding = Utf8NoBom,
                       OmitXmlDeclaration = true
                   }))
        {
            document.WriteTo(tw);
        }
        //document.Save(ms);

        ms.Seek(0, SeekOrigin.Begin);
    }

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

}
