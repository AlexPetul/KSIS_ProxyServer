using System;

namespace KSIS_LAB_4.HTTP
{
    public class ItemBase
    {
        private string _Source = String.Empty;

        public string Source
        {
            get
            {
                return _Source;
            }
        }

        public ItemBase(string source)
        {
            _Source = source;
        }

    }
}
