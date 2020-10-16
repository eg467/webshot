using System;
using WebShot.Menu.ColoredConsole;

namespace WebShot.Menu.Options
{
    public class OptionPrompt : IOutput
    {
        public ColoredOutput Descriptor { get; } = new ColoredOutput("", ConsoleColor.Cyan);
        public ColoredOutput Explanation { get; } = new ColoredOutput("", ConsoleColor.Gray);

        /// <summary>
        /// Creates a prompt with custom colors.
        /// </summary>
        /// <param name="descriptor"></param>
        /// <param name="explanation"></param>
        public OptionPrompt(string descriptor, string explanation)
        {
            Descriptor.Text = descriptor;
            Explanation.Text = explanation;
        }

        private IOutput CombinedOutput
        {
            get
            {
                var descriptorLabel = Descriptor.Length > 0
                    ? Descriptor.FormatLines("{0}) ")
                    : ColoredOutput.Empty;
                return MixedOutput.Horizontal(new[] { descriptorLabel, Explanation });
            }
        }

        public string Text => CombinedOutput.Text;

        public int MaxLineLength => CombinedOutput.MaxLineLength;

        public void Write() => CombinedOutput.Write();

        public void WriteLine() => CombinedOutput.WriteLine();
    }
}