using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Text;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WebShot.Menu.ColoredConsole;
using WebShot.Menu.Options;
using WebshotService;

namespace WebShot.Menu.Menus
{
    public class FactoryMenu<TOutput> : IMenu
    {
        private string _header;
        private readonly Func<ConverterFactory, TOutput> _creator;
        private readonly CustomOption<TOutput> _option;

        public FactoryMenu(
            string header,
            Func<ConverterFactory, TOutput> creator,
            AsyncOptionHandler<TOutput> asyncHandler,
            CompletionHandler completionHandler)
        {
            _header = header;
            _creator = creator;
            _option = new(null, _ => true, asyncHandler, completionHandler);
        }

        public FactoryMenu(
            string header,
            Func<ConverterFactory, TOutput> creator,
            OptionHandler<TOutput> handler,
            CompletionHandler completionHandler)
            : this(header, creator, handler.ToAsync(), completionHandler)
        {
        }

        public async Task<CompletionHandler> DisplayAsync()
        {
            new ColoredOutput(_header).PrintHeader();
            var output = _creator(new ConverterFactory());
            await _option.Execute(output);
            return _option.CompletionHandler;
        }
    }

    public class Maybe
    {
        public static Maybe<T> From<T>(T value) => new(value);

        public static Maybe<T> Empty<T>() => new();
    }

    public class Maybe<T> : IEnumerable<T>
    {
        private readonly T[] _value;

        public Maybe(T value)
        {
            _value = new[] { value };
        }

        public Maybe()
        {
            _value = Array.Empty<T>();
        }

        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_value).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _value.GetEnumerator();
    }

    public static class MaybeExtensions
    {
        public static Maybe<T> AsMaybe<T>(this IEnumerable<T> items) =>
            items.Any() ? new(items.Single()) : new();

        /// <summary>
        /// Converts a non-nullable string to null if it is empty.
        /// </summary>
        /// <param name="item"></param>
        /// <returns>Null if <paramref name="item"/> is an empty string or <paramref name="item"/> otherwise.</returns>
        public static string? EmptyToNull(this string item) =>
            item.Length > 0 ? item : null;
    }

    public class ConverterFactory
    {
        public FactoryMenuConverter New(
                string prompt,
                Func<StringValidator, StringValidator> inputValidator) =>
            new(prompt, inputValidator(new()));

        public FactoryMenuConverter New(string prompt) => new(prompt);
    }

    public delegate string? InputTransformer(string? input);

    public class FactoryMenuConverter
    {
        private readonly string _prompt;
        private readonly StringValidator _validator;

        private const ConsoleColor DefaultValueColor = ConsoleColor.Yellow;
        private const ConsoleColor PromptColor = ConsoleColor.Gray;
        private const ConsoleColor PromptNameColor = ConsoleColor.White;

        public FactoryMenuConverter(string prompt, StringValidator validator)
        {
            _prompt = prompt;
            _validator = validator;
        }

        public FactoryMenuConverter(string prompt) : this(prompt, new())
        {
        }

        /// <summary>
        /// Helper function to convert null/optional default values to an empty <see cref="Maybe{T}"/>.
        /// </summary>
        private T ProcessInputInternal<T>(
                Func<string, T> converter,
                Maybe<T>? defaultIfEmpty = null,
                bool multiline = false) =>
            ProcessInput<T>(converter, defaultIfEmpty ?? Maybe.Empty<T>(), multiline);

        /// <summary>
        /// Inputs user data, and
        /// </summary>
        /// <typeparam name="T">The type of the returned object.</typeparam>
        /// <param name="converter">
        ///     A function that converts the input string to another type.
        ///     Throws <see cref="MenuInputException"/> on invalid input.
        /// </param>
        /// <param name="defaultIfEmpty">The value to return if the string is empty.</param>
        /// <param name="multiline">True if the user should keep entering lines until pressing CTRL-Z.</param>
        ///
        /// <returns></returns>
        public T ProcessInput<T>(Func<string, T> converter, Maybe<T> defaultIfEmpty, bool multiline = false)
        {
            do
            {
                string input = multiline ? GetMultilineResponse() : GetResponse();

                if (input.Length == 0 && defaultIfEmpty.Any())
                    return defaultIfEmpty.Single();

                try
                {
                    _validator.EnsureValid(input);
                    return converter(input);
                }
                catch (AggregateException ex)
                {
                    WriteExceptions(ex.InnerExceptions);
                }
                catch (Exception ex)
                {
                    WriteExceptions(new[] { ex });
                }
            } while (true);

            void PrintPrompt()
            {
                var prompts = new List<IOutput>
                {
                    new ColoredOutput(_prompt, PromptNameColor)
                };

                if (defaultIfEmpty.Any())
                {
                    prompts.Add(new ColoredOutput(" [default: ", PromptColor));
                    var defaultValue = defaultIfEmpty.Single();
                    var serializedDefault = JsonConvert.SerializeObject(defaultValue);
                    prompts.Add(new ColoredOutput(serializedDefault, DefaultValueColor));
                    prompts.Add(new ColoredOutput("]", PromptColor));
                }
                prompts.Add(new ColoredOutput(": ", PromptColor));
                MixedOutput.Horizontal(prompts).Write();
            }

            string GetMultilineResponse()
            {
                PrintPrompt();
                new ColoredOutput("\r\nPress 'Enter' after each item and enter empty line when finished.", PromptColor).WriteLine();
                const int maxLines = 50;
                // A line that isn't the terminal.
                static bool IsNonTerminalLine(string? line) => !string.IsNullOrEmpty(line);

                var lines = Enumerable.Repeat<string?>(null, maxLines)
                    .Select(_ => Console.ReadLine())
                    .TakeWhile(IsNonTerminalLine)
                    .ToArray();

                // CTRL+Z pressed in console on first element.
                if (!lines.Any())
                    throw new OperationCanceledException();

                return string.Join(Environment.NewLine, lines);
            }

            string GetResponse()
            {
                PrintPrompt();
                string? response = Console.ReadLine();

                // CTRL+Z pressed in console
                if (response is null)
                    throw new OperationCanceledException();

                return response;
            }
        }

        private static void WriteExceptions(IEnumerable<Exception> exceptions)
        {
            exceptions
                .Select(e => ColoredOutput.Error(e.Message))
                .ForEach(e => e.WriteLine());
            MixedOutput.BlankLines(1).WriteLine();
        }

        private static void EnsureRange<T>(T val, NumRange<T>? range) where T : struct, IComparable<T>
        {
            if (range?.Contains(val) == false)
            {
                var label = $"Value should be in this range: {range}";
                throw new MenuInputException(label);
            }
        }

        #region Getters

        public bool Bool(Maybe<bool>? defaultValue = null) =>
            ProcessInputInternal(
                response =>
                {
                    var trueVals = "|true|t|yes|";
                    var falseVals = "|false|f|no|";
                    var q = $"|{response}|";
                    var isTrue = trueVals.Contains(q, StringComparison.OrdinalIgnoreCase);
                    var isFalse = falseVals.Contains(q);

                    if (!isTrue && !isFalse)
                    {
                        var validValues = trueVals.Split('|', StringSplitOptions.RemoveEmptyEntries)
                            .Concat(falseVals.Split('|', StringSplitOptions.RemoveEmptyEntries));
                        var validValueDescription = string.Join(", ", validValues);
                        throw new MenuInputException($"The value must be one of the following: {validValueDescription}");
                    }

                    return isTrue;
                },
                defaultValue);

        public int Int(Maybe<int>? defaultValue = null, NumRange<int>? range = null) =>
            ProcessInputInternal(
                response =>
                {
                    try
                    {
                        int i = int.Parse(response);
                        EnsureRange(i, range);
                        return i;
                    }
                    catch (FormatException ex)
                    {
                        throw new MenuInputException("The value must be an integer number.", ex);
                    }
                },
                defaultValue);

        /// <summary>
        ///
        /// </summary>
        /// <param name="range"></param>
        /// <returns></returns>
        public double Dbl(Maybe<double>? defaultValue = null, NumRange<double>? range = null) =>
            ProcessInputInternal(
                response =>
                {
                    var d = double.Parse(response);
                    EnsureRange(d, range);
                    return d;
                },
                defaultValue);

        /// <summary>
        /// Gets a validated, user-inputed string.
        /// </summary>
        /// <param name="defaultValue">The value that replaces an empty response.</param>
        /// <param name="selector">Transforms the user input into another string.</param>
        /// <param name="multiline">Should the program keep prompting for multiple lines</param>
        /// <returns>A validated user-inputted string.</returns>
        public string Str(
                Maybe<string>? defaultValue = null,
                bool multiline = false) =>
            ProcessInputInternal(response => response, defaultValue, multiline);

        public T Select<T>(
                Func<string, T> selector,
                Maybe<T>? defaultValue = null,
                Predicate<T>? outputValidator = null) =>
            ProcessInputInternal(
                response =>
                {
                    T result = selector(response);

                    if (outputValidator?.Invoke(result) == false)
                        throw new MenuInputException("The transformed object is invalid.");

                    return result;
                },
                defaultValue, false);

        public T MultilineSelect<T>(
            Func<string[], T> selector,
            Maybe<T>? defaultValue = null,
            Predicate<T>? outputValidator = null) =>
        ProcessInputInternal(
            response =>
            {
                var responseLines = response.Split(Environment.NewLine);
                T result = selector(responseLines);

                if (outputValidator?.Invoke(result) == false)
                    throw new MenuInputException("The transformed object is invalid.");

                return result;
            },
            defaultValue, true);

        #endregion Getters
    }
}