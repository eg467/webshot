using WebShot.Menu.ColoredConsole;

namespace WebShot.Menu.Menus
{
    public record MenuOutput
    {
        public string Header { get; init; }
        public IOutput Description { get; init; } = ColoredOutput.Empty;

        public MenuOutput(string header)
        {
            Header = header;
        }

        public MenuOutput(string header, IOutput description) : this(header)
        {
            Description = description;
        }
    }
}