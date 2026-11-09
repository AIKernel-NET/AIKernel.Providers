namespace AIKernel.Providers.DynamicPipelineCompiler;

internal static class DynamicPipelineConditionExpressionValidator
{
    public static DynamicPipelineConditionValidationResult Validate(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return DynamicPipelineConditionValidationResult.Valid("empty");
        }

        var tokenizer = new Tokenizer(expression);
        var tokens = tokenizer.Tokenize();
        if (tokens.Error is not null)
        {
            return DynamicPipelineConditionValidationResult.Invalid(tokens.Error);
        }

        var parser = new Parser(tokens.Tokens);
        return parser.Parse();
    }

    private sealed record TokenStream(IReadOnlyList<Token> Tokens, string? Error);

    private sealed record Token(TokenKind Kind, string Text, int Position);

    private enum TokenKind
    {
        Identifier,
        Number,
        String,
        Boolean,
        Operator,
        And,
        Or,
        Not,
        LParen,
        RParen,
        End
    }

    private sealed class Tokenizer(string expression)
    {
        public TokenStream Tokenize()
        {
            var tokens = new List<Token>();
            var index = 0;
            while (index < expression.Length)
            {
                var current = expression[index];
                if (char.IsWhiteSpace(current))
                {
                    index++;
                    continue;
                }

                if (current == '(')
                {
                    tokens.Add(new Token(TokenKind.LParen, "(", index++));
                    continue;
                }

                if (current == ')')
                {
                    tokens.Add(new Token(TokenKind.RParen, ")", index++));
                    continue;
                }

                if (current == '!' && !Peek(index + 1, '='))
                {
                    tokens.Add(new Token(TokenKind.Not, "!", index++));
                    continue;
                }

                if (current is '\'' or '"')
                {
                    var token = ReadString(index, current);
                    if (token.Error is not null)
                    {
                        return token;
                    }

                    tokens.Add(token.Tokens[0]);
                    index = token.Tokens[0].Position + token.Tokens[0].Text.Length + 2;
                    continue;
                }

                if (IsOperatorStart(current))
                {
                    var op = ReadOperator(index);
                    if (op is null)
                    {
                        return new TokenStream(tokens, $"Unsupported operator at position {index}.");
                    }

                    tokens.Add(op);
                    index += op.Text.Length;
                    continue;
                }

                if (char.IsDigit(current) || (current is '+' or '-' && index + 1 < expression.Length && char.IsDigit(expression[index + 1])))
                {
                    var number = ReadNumber(index);
                    tokens.Add(number);
                    index += number.Text.Length;
                    continue;
                }

                if (IsIdentifierStart(current))
                {
                    var identifier = ReadIdentifier(index);
                    var kind = identifier.Text.ToUpperInvariant() switch
                    {
                        "AND" => TokenKind.And,
                        "OR" => TokenKind.Or,
                        "NOT" => TokenKind.Not,
                        "TRUE" or "FALSE" => TokenKind.Boolean,
                        _ => TokenKind.Identifier
                    };
                    tokens.Add(identifier with { Kind = kind });
                    index += identifier.Text.Length;
                    continue;
                }

                return new TokenStream(tokens, $"Unexpected character '{current}' at position {index}.");
            }

            tokens.Add(new Token(TokenKind.End, string.Empty, expression.Length));
            return new TokenStream(tokens, null);
        }

        private bool Peek(int index, char expected)
            => index < expression.Length && expression[index] == expected;

        private static bool IsIdentifierStart(char value)
            => char.IsLetter(value) || value == '_' || value == '$';

        private static bool IsIdentifierPart(char value)
            => char.IsLetterOrDigit(value) || value is '_' or '.' or '-' or '$';

        private static bool IsOperatorStart(char value)
            => value is '=' or '!' or '<' or '>' or '&' or '|';

        private TokenStream ReadString(int start, char quote)
        {
            var index = start + 1;
            while (index < expression.Length)
            {
                if (expression[index] == '\\')
                {
                    index += 2;
                    continue;
                }

                if (expression[index] == quote)
                {
                    var text = expression.Substring(start + 1, index - start - 1);
                    return new TokenStream([new Token(TokenKind.String, text, start)], null);
                }

                index++;
            }

            return new TokenStream([], $"Unterminated string literal at position {start}.");
        }

        private Token? ReadOperator(int start)
        {
            if (start + 1 < expression.Length)
            {
                var two = expression.Substring(start, 2);
                if (two is "==" or "!=" or ">=" or "<=")
                {
                    return new Token(TokenKind.Operator, two, start);
                }

                if (two == "&&")
                {
                    return new Token(TokenKind.And, two, start);
                }

                if (two == "||")
                {
                    return new Token(TokenKind.Or, two, start);
                }
            }

            return expression[start] is '>' or '<' or '='
                ? new Token(TokenKind.Operator, expression[start].ToString(), start)
                : null;
        }

        private Token ReadNumber(int start)
        {
            var index = start;
            if (expression[index] is '+' or '-')
            {
                index++;
            }

            while (index < expression.Length && char.IsDigit(expression[index]))
            {
                index++;
            }

            if (index < expression.Length && expression[index] == '.')
            {
                index++;
                while (index < expression.Length && char.IsDigit(expression[index]))
                {
                    index++;
                }
            }

            return new Token(TokenKind.Number, expression[start..index], start);
        }

        private Token ReadIdentifier(int start)
        {
            var index = start;
            while (index < expression.Length && IsIdentifierPart(expression[index]))
            {
                index++;
            }

            return new Token(TokenKind.Identifier, expression[start..index], start);
        }
    }

    private sealed class Parser(IReadOnlyList<Token> tokens)
    {
        private int _index;

        public DynamicPipelineConditionValidationResult Parse()
        {
            var result = ParseOr();
            if (result.Error is not null)
            {
                return DynamicPipelineConditionValidationResult.Invalid(result.Error);
            }

            if (Current.Kind != TokenKind.End)
            {
                return DynamicPipelineConditionValidationResult.Invalid(
                    $"Unexpected token '{Current.Text}' at position {Current.Position}.");
            }

            return DynamicPipelineConditionValidationResult.Valid("boolean-v1");
        }

        private Token Current => tokens[_index];

        private ParseResult ParseOr()
        {
            var left = ParseAnd();
            if (left.Error is not null)
            {
                return left;
            }

            while (Current.Kind == TokenKind.Or)
            {
                Advance();
                var right = ParseAnd();
                if (right.Error is not null)
                {
                    return right;
                }
            }

            return ParseResult.Valid;
        }

        private ParseResult ParseAnd()
        {
            var left = ParseUnary();
            if (left.Error is not null)
            {
                return left;
            }

            while (Current.Kind == TokenKind.And)
            {
                Advance();
                var right = ParseUnary();
                if (right.Error is not null)
                {
                    return right;
                }
            }

            return ParseResult.Valid;
        }

        private ParseResult ParseUnary()
        {
            if (Current.Kind == TokenKind.Not)
            {
                Advance();
                return ParseUnary();
            }

            return ParsePrimary();
        }

        private ParseResult ParsePrimary()
        {
            if (Current.Kind == TokenKind.LParen)
            {
                Advance();
                if (Current.Kind == TokenKind.RParen)
                {
                    return ParseResult.Invalid("Empty parentheses are not a valid boolean expression.");
                }

                var inner = ParseOr();
                if (inner.Error is not null)
                {
                    return inner;
                }

                if (Current.Kind != TokenKind.RParen)
                {
                    return ParseResult.Invalid($"Missing ')' before position {Current.Position}.");
                }

                Advance();
                return ParseResult.Valid;
            }

            return ParsePredicate();
        }

        private ParseResult ParsePredicate()
        {
            var left = ParseOperand();
            if (left.Error is not null)
            {
                return left;
            }

            if (Current.Kind != TokenKind.Operator)
            {
                return ParseResult.Valid;
            }

            Advance();
            var right = ParseOperand();
            return right.Error is null
                ? ParseResult.Valid
                : right;
        }

        private ParseResult ParseOperand()
        {
            if (Current.Kind is TokenKind.Identifier or TokenKind.Number or TokenKind.String or TokenKind.Boolean)
            {
                Advance();
                return ParseResult.Valid;
            }

            return ParseResult.Invalid($"Expected operand at position {Current.Position}.");
        }

        private void Advance()
        {
            if (_index < tokens.Count - 1)
            {
                _index++;
            }
        }
    }

    private sealed record ParseResult(string? Error)
    {
        public static ParseResult Valid { get; } = new((string?)null);

        public static ParseResult Invalid(string error) => new(error);
    }
}

internal sealed record DynamicPipelineConditionValidationResult(
    bool IsValid,
    string Grammar,
    string? Error)
{
    public static DynamicPipelineConditionValidationResult Valid(string grammar)
        => new(true, grammar, null);

    public static DynamicPipelineConditionValidationResult Invalid(string error)
        => new(false, "boolean-v1", error);
}
