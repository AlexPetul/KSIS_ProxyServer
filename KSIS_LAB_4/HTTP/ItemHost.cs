using System;
using System.Text.RegularExpressions;

namespace KSIS_LAB_4.HTTP
{
    public class ItemHost : ItemBase
  {

    private string _Host = String.Empty;
    private int _Port = 80;

    /// <summary>
    /// Адрес хоста (домен)
    /// </summary>
    public string Host
    {
      get
      {
        return _Host;
      }
    }

    /// <summary>
    /// Номер порта, по умолчанию - 80
    /// </summary>
    public int Port
    {
      get
      {
        return _Port;
      }
    }

    public ItemHost(string source) : base(source)
    {
      // пасим данные
      Regex myReg = new Regex(@"^(((?<host>.+?):(?<port>\d+?))|(?<host>.+?))$");
      Match m = myReg.Match(source);
      _Host = m.Groups["host"].Value;
      if (!int.TryParse(m.Groups["port"].Value, out _Port))
      { // не удалось преобразовать порт в число, значит порт не указан, ставим значение по умолчанию
        _Port = 80;
      }
    }

  }
}
