namespace Vodostaji.Ingest;

/// <summary>
/// Odgovor izvora se nije mogao pročitati kao cjelina.
///
/// Ruši jedan run jednog izvora, nikad ostale (zlatno pravilo 5). Zato živi u korijenu
/// `Vodostaji.Ingest`, a ne kod pojedinog adaptera — svaki izvor može pasti na isti način.
/// </summary>
public sealed class SourceResponseException(string message) : Exception(message);
