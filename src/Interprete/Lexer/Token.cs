namespace Interprete.Lexing;

//Unidad minima que produce el Lexer.
//Line y Column sirven para los mensajes de error. 
public record Token(TokenType Type, string Literal, int Line, int Column);
