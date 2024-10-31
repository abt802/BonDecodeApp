using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCardCheckerGui
{
    internal class StringBuilderIndent
    {
        public int Indent { get; set; } = 0;

        public void AddLog(string text) => sb.AppendLine(new string(' ', Indent) + text);

        public override string ToString() => sb.ToString();

        public void Clear() => sb.Clear();

        private StringBuilder sb = new StringBuilder();
    }
}
