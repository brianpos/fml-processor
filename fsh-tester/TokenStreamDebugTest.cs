using Microsoft.VisualStudio.TestTools.UnitTesting;
using Antlr4.Runtime;
using fsh_processor.antlr;
using System.IO;
using System.Text;

namespace fsh_tester;

[TestClass]
public class TokenStreamDebugTest
{
    [TestMethod]
    public void InspectTokenStreamForComments()
    {
        var fshText = @"Profile: MyProfile
Parent: Patient

* name 1..1 MS // name is required
* identifier 0..* MS
";

        var inputStream = new AntlrInputStream(fshText);
        var lexer = new FSHLexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        tokenStream.Fill();

        var sb = new StringBuilder();
        sb.AppendLine("=== ALL TOKENS ===");
        var tokens = tokenStream.GetTokens();
        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            var channelName = token.Channel == Lexer.DefaultTokenChannel ? "DEFAULT" : 
                             token.Channel == Lexer.Hidden ? "HIDDEN" : 
                             token.Channel.ToString();
            var text = token.Text.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
            sb.AppendLine($"Token {i}: Channel={channelName}, Type={token.Type}, Text='{text}'");
        }
        
        File.WriteAllText(@"C:\temp\token_stream_debug.txt", sb.ToString());
    }
}
