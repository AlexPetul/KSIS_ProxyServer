using System;
using System.Text;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.IO;

namespace KSIS_LAB_4.HTTP
{
    public class Parser
    {
        public enum MethodsList
        {
            /// <summary>
            /// Запрос методом GET
            /// </summary>
            GET,
            /// <summary>
            /// Запрос методом POST
            /// </summary>
            POST,
            /// <summary>
            /// Для защищенных соединений
            /// </summary>
            CONNECT
        }

        private MethodsList _Method = MethodsList.GET;
        private string _HTTPVersion = "1.1";
        private ItemCollection _Items = null;
        private string _Path = String.Empty;
        private byte[] _Source = null;

        private int _StatusCode = 0;
        private string _StatusMessage = string.Empty;

        private int _HeadersTail = -1;

        /// <summary>
        /// Тип запроса (при запросе)
        /// </summary>
        public MethodsList Method
        {
            get
            {
                return _Method;
            }
        }

        /// <summary>
        /// Путь (при запросе)
        /// </summary>
        public string Path
        {
            get
            {
                return _Path;
            }
        }

        /// <summary>
        /// Заголовки
        /// </summary>
        public ItemCollection Items
        {
            get
            {
                return _Items;
            }
        }

        /// <summary>
        /// Источник данных
        /// </summary>
        public byte[] Source
        {
            get
            {
                return _Source;
            }
        }

        /// <summary>
        /// Хост (домен, ip) - при запросе
        /// </summary>
        public string Host
        {
            get
            {
                if (!_Items.ContainsKey("Host")) return String.Empty;
                return ((ItemHost)_Items["Host"]).Host;
            }
        }

        /// <summary>
        /// Номер порта, по умолчанию 80 - при запросе
        /// </summary>
        public int Port
        {
            get
            {
                if (!_Items.ContainsKey("Host")) return 80;
                return ((ItemHost)_Items["Host"]).Port;
            }
        }

        /// <summary>
        /// Код состояния (при ответе)
        /// </summary>
        public int StatusCode
        {
            get
            {
                return _StatusCode;
            }
        }

        /// <summary>
        /// Сообщение сервера (при ответе)
        /// </summary>
        public string StatusMessage
        {
            get
            {
                return _StatusMessage;
            }
        }

        public Parser(byte[] source)
        {
            if (source == null || source.Length <= 0) return;
            _Source = source;

            _Items = new ItemCollection();
            // преобразуем данные в текст
            string sourceString = GetSourceAsString();

            // при запросе
            // первая строка содержит метод запроса, путь и версию HTTP протокола
            string httpInfo = sourceString.Substring(0, sourceString.IndexOf("\r\n"));
            Regex myReg = new Regex(@"(?<method>.+)\s+(?<path>.+)\s+HTTP/(?<version>[\d\.]+)", RegexOptions.Multiline);
            if (myReg.IsMatch(httpInfo))
            {
                Match m = myReg.Match(httpInfo);
                if (m.Groups["method"].Value.ToUpper() == "POST")
                {
                    _Method = MethodsList.POST;
                }
                else if (m.Groups["method"].Value.ToUpper() == "CONNECT")
                {
                    _Method = MethodsList.CONNECT;
                }
                else
                {
                    _Method = MethodsList.GET;
                }
                // или можно определить метод вот так
                // _Method = (MethodsList)Enum.Parse(typeof(MethodsList), m.Groups["method"].Value.ToUpper());
                // но надежней всеже использовать условие

                _Path = m.Groups["path"].Value;
                _HTTPVersion = m.Groups["version"].Value;
            }
            else
            {
                // при ответе
                // первая строка содержит код состояния
                myReg = new Regex(@"HTTP/(?<version>[\d\.]+)\s+(?<status>\d+)\s*(?<msg>.*)", RegexOptions.Multiline);
                Match m = myReg.Match(httpInfo);
                int.TryParse(m.Groups["status"].Value, out _StatusCode);
                _StatusMessage = m.Groups["msg"].Value;
                _HTTPVersion = m.Groups["version"].Value;
            }

            // выделяем заголовки (до первых двух переводов строк)
            _HeadersTail = sourceString.IndexOf("\r\n\r\n");
            if (_HeadersTail != -1)
            { // хвост найден, отделяем заголовки
                sourceString = sourceString.Substring(sourceString.IndexOf("\r\n") + 2, _HeadersTail - sourceString.IndexOf("\r\n") - 2);
            }

            // парсим заголовки и заносим их в коллекцию
            myReg = new Regex(@"^(?<key>[^\x3A]+)\:\s{1}(?<value>.+)$", RegexOptions.Multiline);
            MatchCollection mc = myReg.Matches(sourceString);
            foreach (Match mm in mc)
            {
                string key = mm.Groups["key"].Value;
                if (!_Items.ContainsKey(key))
                {
                    // если указанного заголовка нет в коллекции, добавляем его
                    _Items.AddItem(key, mm.Groups["value"].Value.Trim("\r\n ".ToCharArray()));
                }
            }
        }

        /// <summary>
        /// Возвращает оригинальные данные в виде строки
        /// </summary>
        public string GetSourceAsString()
        {
            Encoding e = Encoding.UTF8;
            // если есть тип содержимого, то может есть и кодировка
            if (_Items != null && _Items.ContainsKey("Content-Type") && !String.IsNullOrEmpty(((ItemContentType)_Items["Content-Type"]).Charset))
            {
                // кодировка есть, используем её при декодировании данных
                try
                {
                    e = Encoding.GetEncoding(((ItemContentType)_Items["Content-Type"]).Charset);
                }
                catch { }
            }
            return e.GetString(_Source);
        }

        /// <summary>
        /// Возвращает заголовки в виде строки
        /// </summary>
        /// <returns></returns>
        public string GetHeadersAsString()
        {
            if (_Items == null) return String.Empty;
            return _Items.ToString();
        }

        /// <summary>
        /// Фукнция возвразает содержимое в виде масссива байт
        /// </summary>
        public byte[] GetBody()
        {
            // хвоста нет, значит тела тоже нет
            if (_HeadersTail == -1) return null;
            // выделяем тело, начиная с конца хвоста
            byte[] result = new byte[_Source.Length - _HeadersTail - 4];
            Buffer.BlockCopy(_Source, _HeadersTail + 4, result, 0, result.Length);
            // тело может быть сжато, проверяем так это или нет
            if (_Items != null && _Items.ContainsKey("Content-Encoding") && _Items["Content-Encoding"].Source.ToLower() == "gzip")
            {
                // если данные сжаты, то разархивируем их
                GZipStream myGzip = new GZipStream(new MemoryStream(result), CompressionMode.Decompress);
                using (MemoryStream m = new MemoryStream())
                {
                    byte[] buffer = new byte[512];
                    int len = 0;
                    while ((len = myGzip.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        m.Write(buffer, 0, len);
                    }
                    result = m.ToArray();
                }
            }
            // возвращаем результат
            return result;
        }

        /// <summary>
        /// Функция возвращает содержимое в виде строки
        /// </summary>
        public string GetBodyAsString()
        {
            Encoding e = Encoding.UTF8;
            // если есть тип содержимого, то может есть и кодировка
            if (_Items != null && _Items.ContainsKey("Content-Type") && !String.IsNullOrEmpty(((ItemContentType)_Items["Content-Type"]).Charset))
            {
                // кодировка есть, используем её при декодировании содержимого
                try
                {
                    e = Encoding.GetEncoding(((ItemContentType)_Items["Content-Type"]).Charset);
                }
                catch { }
            }
            return e.GetString(GetBody());
        }

        /// <summary>
        /// Вставляет текстовое содержимое, изменяет Content-Length
        /// </summary>
        /// <param name="newBody">Новое содержимое, которое нужно вставить</param>
        public void SetStringBody(string newBody)
        {
            // формируем заголовки
            if (_StatusCode <= 0)
            {
                // такого быть не должно
                throw new Exception("Можно изменять только содержимое, полученное в ответ от удаленного сервера.");
            }
            Encoding e = Encoding.UTF8;
            string result = String.Format("HTTP/{0} {1} {2}", _HTTPVersion, _StatusCode, _StatusMessage);
            foreach (string k in _Items.Keys)
            {
                ItemBase itm = _Items[k];
                if (!String.IsNullOrEmpty(result)) result += "\r\n";
                if (k.ToLower() == "content-length")
                {
                    // информация о размере содержимого, меняем
                    result += String.Format("{0}: {1}", k, newBody.Length);
                }
                else if (k.ToLower() == "content-encoding" && itm.Source.ToLower() == "gzip")
                {
                    // если оригинальное содержимое сжато, то пропускаем этот заголовок, т.к. у нас обратного сжатия нет (но можно сделать, если нужно)
                }
                else
                {
                    // другие заголовки оставляем без изменений
                    result += String.Format("{0}: {1}", k, itm.Source);
                    // если это тип содержимого, то смотрим, может есть информация о кодировке
                    if (k.ToLower() == "content-type" && !String.IsNullOrEmpty(((ItemContentType)_Items["Content-Type"]).Charset))
                    {
                        // кодировка есть, используем её при кодировании содержимого
                        try
                        {
                            e = Encoding.GetEncoding(((ItemContentType)_Items["Content-Type"]).Charset);
                        }
                        catch { }
                    }
                }
            }
            // разделитель между телом и заголовками
            result += "\r\n\r\n";
            // добавляем тело
            result += newBody;
            // переносим в источник
            _Source = e.GetBytes(result);
        }

    }
}
