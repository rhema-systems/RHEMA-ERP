namespace ErpSystem.Api.Services.Finance.Reporting;

internal sealed record FinancialStatementFormulaParseResult(
    bool IsValid,
    IReadOnlySet<string> Dependencies,
    string? Error);

internal sealed record FinancialStatementFormulaEvaluationResult(
    bool IsValid,
    decimal Value,
    string? Error);

/// <summary>
/// Parses the deliberately small financial-statement formula language.
/// Formula text is never compiled or executed as application code.
/// </summary>
internal static class FinancialStatementFormulaParser
{
    public static FinancialStatementFormulaParseResult Parse(
        string? formula,
        IReadOnlyList<string> orderedRowCodes)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return new(false, new HashSet<string>(), "Formula is required.");
        }

        try
        {
            var parser = new Parser(formula, orderedRowCodes, _ => 0m);
            var (_, dependencies) = parser.Parse();
            return new(true, dependencies, null);
        }
        catch (FormulaParseException exception)
        {
            return new(false, new HashSet<string>(), exception.Message);
        }
    }

    public static FinancialStatementFormulaEvaluationResult Evaluate(
        string? formula,
        IReadOnlyList<string> orderedRowCodes,
        Func<string, decimal> rowValueResolver)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return new(false, 0m, "Formula is required.");
        }

        try
        {
            var parser = new Parser(formula, orderedRowCodes, rowValueResolver);
            var (value, _) = parser.Parse();
            return new(true, value, null);
        }
        catch (FormulaParseException exception)
        {
            return new(false, 0m, exception.Message);
        }
    }

    private enum TokenType
    {
        Identifier,
        Plus,
        Minus,
        LeftParenthesis,
        RightParenthesis,
        Colon,
        End
    }

    private sealed record Token(TokenType Type, string Text, int Position);

    private sealed class Parser
    {
        private readonly IReadOnlyList<string> _orderedRowCodes;
        private readonly Func<string, decimal> _rowValueResolver;
        private readonly HashSet<string> _dependencies = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<Token> _tokens;
        private int _position;

        public Parser(
            string formula,
            IReadOnlyList<string> orderedRowCodes,
            Func<string, decimal> rowValueResolver)
        {
            _orderedRowCodes = orderedRowCodes;
            _rowValueResolver = rowValueResolver;
            _tokens = Tokenize(formula);
        }

        public (decimal Value, IReadOnlySet<string> Dependencies) Parse()
        {
            var value = ParseExpression();
            Expect(TokenType.End, "Unexpected text after the formula expression.");
            return (value, _dependencies);
        }

        private decimal ParseExpression()
        {
            var sign = 1m;
            if (Current.Type is TokenType.Plus or TokenType.Minus)
            {
                if (Current.Type == TokenType.Minus)
                {
                    sign = -1m;
                }
                Advance();
            }

            var value = sign * ParseFactor();
            while (Current.Type is TokenType.Plus or TokenType.Minus)
            {
                var operation = Current.Type;
                Advance();
                var operand = ParseFactor();
                value = operation == TokenType.Plus
                    ? value + operand
                    : value - operand;
            }

            return value;
        }

        private decimal ParseFactor()
        {
            if (Current.Type == TokenType.LeftParenthesis)
            {
                Advance();
                var value = ParseExpression();
                Expect(TokenType.RightParenthesis, "A closing parenthesis is required.");
                Advance();
                return value;
            }

            if (Current.Type != TokenType.Identifier)
            {
                throw Error(Current, "Expected a row code, SUM range, or parenthesized expression.");
            }

            if (Current.Text.Equals("SUM", StringComparison.OrdinalIgnoreCase)
                && Peek().Type == TokenType.LeftParenthesis)
            {
                return ParseSumRange();
            }

            var rowCode = Current.Text;
            _dependencies.Add(rowCode);
            Advance();
            return _rowValueResolver(rowCode);
        }

        private decimal ParseSumRange()
        {
            Advance();
            Expect(TokenType.LeftParenthesis, "SUM must be followed by an opening parenthesis.");
            Advance();

            var from = ExpectIdentifier("SUM requires a starting row code.");
            Expect(TokenType.Colon, "SUM ranges must use a colon between row codes.");
            Advance();
            var to = ExpectIdentifier("SUM requires an ending row code.");
            Expect(TokenType.RightParenthesis, "SUM requires a closing parenthesis.");
            Advance();

            var fromIndex = IndexOfRowCode(from);
            var toIndex = IndexOfRowCode(to);
            if (fromIndex < 0 || toIndex < 0)
            {
                var missing = fromIndex < 0 ? from : to;
                throw new FormulaParseException($"SUM references unknown row code '{missing}'.");
            }

            if (fromIndex > toIndex)
            {
                throw new FormulaParseException(
                    $"SUM range '{from}:{to}' is reversed in display order.");
            }

            var value = 0m;
            for (var index = fromIndex; index <= toIndex; index++)
            {
                var rowCode = _orderedRowCodes[index];
                _dependencies.Add(rowCode);
                value += _rowValueResolver(rowCode);
            }

            return value;
        }

        private string ExpectIdentifier(string message)
        {
            Expect(TokenType.Identifier, message);
            var text = Current.Text;
            Advance();
            return text;
        }

        private int IndexOfRowCode(string rowCode)
        {
            for (var index = 0; index < _orderedRowCodes.Count; index++)
            {
                if (_orderedRowCodes[index].Equals(rowCode, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private Token Current => _tokens[_position];

        private Token Peek() => _tokens[Math.Min(_position + 1, _tokens.Count - 1)];

        private void Advance()
        {
            if (_position < _tokens.Count - 1)
            {
                _position++;
            }
        }

        private void Expect(TokenType tokenType, string message)
        {
            if (Current.Type != tokenType)
            {
                throw Error(Current, message);
            }
        }

        private static FormulaParseException Error(Token token, string message)
            => new($"{message} Position {token.Position + 1}.");
    }

    private static List<Token> Tokenize(string formula)
    {
        var tokens = new List<Token>();
        var position = 0;

        while (position < formula.Length)
        {
            var character = formula[position];
            if (char.IsWhiteSpace(character))
            {
                position++;
                continue;
            }

            var tokenType = character switch
            {
                '+' => TokenType.Plus,
                '-' => TokenType.Minus,
                '(' => TokenType.LeftParenthesis,
                ')' => TokenType.RightParenthesis,
                ':' => TokenType.Colon,
                _ => (TokenType?)null
            };

            if (tokenType.HasValue)
            {
                tokens.Add(new Token(tokenType.Value, character.ToString(), position));
                position++;
                continue;
            }

            if (char.IsLetterOrDigit(character) || character is '_' or '.')
            {
                var start = position;
                while (position < formula.Length
                    && (char.IsLetterOrDigit(formula[position]) || formula[position] is '_' or '.'))
                {
                    position++;
                }

                tokens.Add(new Token(
                    TokenType.Identifier,
                    formula[start..position],
                    start));
                continue;
            }

            throw new FormulaParseException(
                $"Unsupported character '{character}' at position {position + 1}.");
        }

        tokens.Add(new Token(TokenType.End, string.Empty, formula.Length));
        return tokens;
    }

    private sealed class FormulaParseException : Exception
    {
        public FormulaParseException(string message) : base(message)
        {
        }
    }
}
