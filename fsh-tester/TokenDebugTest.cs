using Antlr4.Runtime;
using fsh_processor.antlr;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace fsh_tester;

[TestClass]
public class TokenDebugTest
{
    [TestMethod]
    public void DebugParameterizedRuleSetTokens()
    {
        var fshText = "RuleSet: SetStatus(status, temp)";
        
        // Create ANTLR input stream
        var inputStream = new AntlrInputStream(fshText);
        
        // Create lexer
        var lexer = new FSHLexer(inputStream);
        
        // Get all tokens
        var tokens = lexer.GetAllTokens();
        
        Console.WriteLine($"Total tokens: {tokens.Count}");
        Console.WriteLine("\nToken stream:");
        foreach (var token in tokens)
        {
            var tokenName = lexer.Vocabulary.GetSymbolicName(token.Type);
            Console.WriteLine($"  [{token.TokenIndex}] {tokenName,-20} = '{token.Text}' (line {token.Line}:{token.Column})");
        }
    }
}
