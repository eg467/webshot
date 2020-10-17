using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using WebShot.Menu.ColoredConsole;

namespace WebShot.Menu.Options
{
    public class OptionPrompt : IOutput
    {
        private const ConsoleColor DescriptorColor = ConsoleColor.Cyan;
        private const ConsoleColor ParamColor = ConsoleColor.Yellow;
        private const ConsoleColor ExplanationColor = ConsoleColor.Gray;

        public MixedOutput Descriptor { get; }
        public ColoredOutput Explanation { get; }

        public const string ParamStart = "<";
        public const string ParamEnd = ">";

        /// <summary>
        /// Creates a prompt with custom colors.
        /// </summary>
        /// <param name="descriptor"></param>
        /// <param name="explanation"></param>
        public OptionPrompt(string descriptor, string explanation)
        {
            Descriptor = ParseDescriptor(descriptor);
            Explanation = new ColoredOutput(explanation, ExplanationColor);
        }

        /// <summary>
        /// Colorized parameters in descriptor.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="includeIdentifier"></param>
        /// <returns></returns>
        public MixedOutput ParseDescriptor(string text, bool includeIdentifier = true)
        {
            var normal = ColoredOutput.ColoredFactory(DescriptorColor);
            var param = ColoredOutput.ColoredFactory(ParamColor);
            List<ColoredOutput> items = new();

            (int start, int end) FindParam(int startSearch)
            {
                if (startSearch >= text.Length)
                    return (-1, -1);
                var l = text.IndexOf(ParamStart, startSearch);
                if (l < 0)
                    return (-1, -1);
                var r = text.IndexOf(ParamEnd, l);
                return (l, r);
            }

            int idx = 0;
            (int pStart, int pEnd) = FindParam(idx);

            while (idx < text.Length && pEnd >= 0)
            {
                var normalText = text.Substring(idx, pStart - idx);
                items.Add(normal(normalText));

                var paramText = includeIdentifier
                    ? text.Substring(pStart, pEnd - pStart + ParamStart.Length + ParamEnd.Length - 1)
                    : text.Substring(pStart + ParamStart.Length, pEnd - pStart - ParamEnd.Length);

                items.Add(param(paramText));

                idx = pEnd + ParamEnd.Length;

                (pStart, pEnd) = FindParam(idx);
            }

            var restOfText = idx < text.Length ? text.Substring(idx) : "";
            items.Add(normal(restOfText));

            var nonEmptyList = items.Where(i => i.Length > 0);
            return MixedOutput.Horizontal(nonEmptyList);
        }

        private IOutput CombinedOutput
        {
            get
            {
                var leftSeparator = new ColoredOutput("[", ExplanationColor);
                var rightSeparator = new ColoredOutput("] ", ExplanationColor);
                return MixedOutput.Horizontal(new IOutput[] { leftSeparator, Descriptor, rightSeparator, Explanation });
            }
        }

        public string Text => CombinedOutput.Text;

        public int MaxLineLength => CombinedOutput.MaxLineLength;

        public void Write() => CombinedOutput.Write();

        public void WriteLine() => CombinedOutput.WriteLine();
    }
}