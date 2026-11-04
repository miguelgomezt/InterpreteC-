namespace Interprete.Lexing; 

//Convierte el texto fuente en tokens, leyendo caracter por caracter.
public class Lexer{
     private readonly string _input; 
     private int _position;  //Posicion del caracter acutal
     private int _readPosition; //Siguiente posicion a leer
     private char _ch; //caracter actual ('\0' = fin de la entrada)
     private int _line = 1; //Linea del caracter actual
     private int _column = 0; //Columna del caracter actual

     public Lexer(string input){
        _input = input;
        ReadChar();
     }

     //Devuelve el siguiente token de la entrada
     public Token NextToken(){
        SkipWhitespaceAndComments();

        int line = _line; 
        int column = _column;

        Token Make(TokenType type, string literal) => new(type, literal, line, column);

        Token token; 
        switch (_ch){
            case '=':
                token = PeekChar() == '=' ? Make(TokenType.Eq, TwoChars()) : Make(TokenType.Assign, "=");
                break;
            case '!':
                token = PeekChar() == '=' ? Make(TokenType.NotEq, TwoChars()) : Make(TokenType.Bang, "!");
                break;
            case '<':
                token = PeekChar() == '=' ? Make(TokenType.Lte, TwoChars()) : Make(TokenType.Lt, "<");
                break;
            case '>':
                token = PeekChar() == '=' ? Make(TokenType.Gte, TwoChars()) : Make(TokenType.Gt, ">");
                break;
            case '*':
                token = PeekChar() == '*' ? Make(TokenType.Pow, TwoChars()) : Make(TokenType.Asterisk, "*");
                break;
            case '+': token = Make(TokenType.Plus, "+"); break;
            case '-': token = Make(TokenType.Minus, "-"); break;
            case '/': token = Make(TokenType.Slash, "/"); break;
            case '%': token = Make(TokenType.Mod, "%"); break;
            case ',': token = Make(TokenType.Comma, ","); break;
            case ';': token = Make(TokenType.Semicolon, ";"); break;
            case ':': token = Make(TokenType.Colon, ":"); break;
            case '(': token = Make(TokenType.LParen, "("); break;
            case ')': token = Make(TokenType.RParen, ")"); break;
            case '{': token = Make(TokenType.LBrace, "{"); break;
            case '}': token = Make(TokenType.RBrace, "}"); break;
            case '[': token = Make(TokenType.LBracket, "["); break;
            case ']': token = Make(TokenType.RBracket, "]"); break;
            case '"':
                var (text, closed) = ReadString();
                token = closed ? Make(TokenType.String, text) : Make(TokenType.Illegal, "\"" + text);
                break; 
            case '\0':
                return Make(TokenType.Eof, ""); 
            default: 
            if (IsLetter(_ch))
            {
                string ident = ReadIdentifier();
                return Make(Keywords.Lookup(ident), ident);
            }
            if (char.IsAsciiDigit(_ch))
            {
                return Make (TokenType.Int, ReadNumber());
            }
            token = Make(TokenType.Illegal, _ch.ToString());
            break;
        }

        ReadChar();
        return token; 
     }

     //Avanza un caracter y actualiza line y columna 
     private void ReadChar()
     {
        if(_ch == '\n')
        {
            _line++;
            _column = 0; 
        }

        _ch = _readPosition >= _input.Length ? '\0' : _input[_readPosition];
        _position = _readPosition;
        _readPosition++;
        _column++;
     }

     //Mira el siguiente caracter sin avanzar
     private char PeekChar() =>
        _readPosition >= _input.Length ? '\0' : _input[_readPosition];

    //Para operadores de dos caracteres: ==, !=, <=, >=, **
    private string TwoChars(){
        char first = _ch; 
        ReadChar();
        return $"{first}{_ch}";
    }

    private string ReadIdentifier()
    {
        int start = _position; 
        while (IsLetter(_ch) || char.IsAsciiDigit(_ch))
        {
            ReadChar();
        }
        return _input[start.._position];
    }

    private string ReadNumber()
    {
        int start = _position;
        while (char.IsAsciiDigit(_ch))
        {
            ReadChar();
        }
        return _input[start.._position];
    }

    //Lee una cadena entre comillas. Closed = false si nunca se cerro. 
    private(string text, bool closed) ReadString()
    {
        int start = _position + 1; 
        while (true)
        {
            ReadChar();
            if (_ch == '"') return (_input[start.._position], true);
            if (_ch == '\0') return (_input[start.._position], false);
        }
    }

    //Salta espacios, saltos de linea y comentarios de una linea (//...)
    private void SkipWhitespaceAndComments()
    {
        while(true)
        {
            if (_ch == ' ' || _ch == '\t' || _ch == '\n' || _ch == '\r')
            {
                ReadChar();
            }
            else if (_ch == '/' && PeekChar() == '/')
            {
                while (_ch != '\n' && _ch != '\0') ReadChar();
            }
            else
            {
                return;
            }
        }
    }

    private static bool IsLetter(char ch) => char.IsAsciiLetter(ch) || ch == '_';
}
