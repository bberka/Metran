namespace Metran.Samples;

/// <summary>
/// This is global container for Metran transactions.
/// This is not recommended for use.
/// Metran transactions are meant to be used in a scoped manner.
///
/// <br></br>
/// <br></br>
/// You should define MetranContainer for each type of transaction identity and action you want to perform.
/// </summary>
public static class TestMetranContainer
{
  public static readonly MetranContainer<long> Container = new();

}