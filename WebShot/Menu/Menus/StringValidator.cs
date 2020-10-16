using OpenQA.Selenium.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;

namespace WebShot.Menu.Menus
{
    // TODO: Use validation attributes in models?
    /// <summary>
    /// Validates raw string input from user.
    /// </summary>
    public class StringValidator
    {
        public static StringValidator MatchAll => new();

        internal class LengthValidator : IInputValidator
        {
            private readonly NumRange<int> _lengthRange;

            public LengthValidator(NumRange<int> lengthRange)
            {
                _lengthRange = lengthRange;
            }

            public string ErrorMessage => $"The string length must be in this range: {_lengthRange}";

            public bool Validate(string input) =>
                _lengthRange.Contains(input.Length);
        }

        internal class IntValidator : IInputValidator
        {
            public string ErrorMessage => "The value must be an integer.";

            public bool Validate(string input) =>
                int.TryParse(input, out var _);
        }

        internal class DoubleValidator : IInputValidator
        {
            public string ErrorMessage => "The value must be numeric.";

            public bool Validate(string input) =>
                double.TryParse(input, out var _);
        }

        internal class RegexValidator : IInputValidator
        {
            private readonly Regex _regex;

            public RegexValidator(
                string pattern,
                bool ignoreCase = true,
                bool matchEntireString = true,
                string errorMessage = "The input failed regular-expression validation.")
            {
                ErrorMessage = errorMessage;
                if (matchEntireString)
                {
                    var start = pattern.StartsWith("^") ? "" : "^";
                    var end = pattern.EndsWith("$") ? "" : "$";
                    pattern = start + pattern + end;
                }
                var options = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
                _regex = new Regex(pattern, options);
            }

            public bool Validate(string input) => _regex.IsMatch(input);

            public string ErrorMessage { get; }
        }

        /// <summary>
        /// checks if a string is a valid reguar expression pattern.
        /// </summary>
        internal class RegexPatternValidator : IInputValidator
        {
            public bool Validate(string input)
            {
                try
                {
                    _ = new Regex(input);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            public string ErrorMessage => "The input is not a valid regular expression pattern.";
        }

        internal class PredicateValidator : IInputValidator
        {
            private readonly Predicate<string> _predicate;

            public PredicateValidator(
                Predicate<string> predicate,
                string error = "The input failed validation.")
            {
                ErrorMessage = error;
                _predicate = predicate;
            }

            public string ErrorMessage { get; }

            public bool Validate(string input) => _predicate(input);
        }

        public class CompositeOrValidator : IInputValidator
        {
            private readonly IInputValidator[] _validators;
            public string ErrorMessage { get; } = "The input failed validation.";

            public CompositeOrValidator(string error, params IInputValidator[] validators)
            {
                ErrorMessage = error ?? ErrorMessage;
                _validators = validators;
            }

            public bool Validate(string input) =>
                !_validators.Any()
                || _validators.Any(v => v.Validate(input));
        }

        private readonly List<IInputValidator> _inputValidators = new();

        public StringValidator Regex(string pattern, bool ignoreCase = true, bool matchEntireString = true)
        {
            _inputValidators.Add(new RegexValidator(pattern, ignoreCase, matchEntireString));
            return this;
        }

        public StringValidator NullOrRegex(string pattern, bool ignoreCase = true, bool matchEntireString = true)
        {
            var regexCheck = new RegexValidator(pattern, ignoreCase, matchEntireString);
            var nullCheck = new PredicateValidator(s => s == null);
            var error = "The input must either be null or match the described pattern.";
            _inputValidators.Add(new CompositeOrValidator(error, regexCheck, nullCheck));
            return this;
        }

        public StringValidator Length(NumRange<int> range)
        {
            _inputValidators.Add(new LengthValidator(range));
            return this;
        }

        public StringValidator NotEmpty()
        {
            NumRange<int> range = NumRange.AtLeast(1);
            _inputValidators.Add(new LengthValidator(range));
            return this;
        }

        public StringValidator Int()
        {
            _inputValidators.Add(new IntValidator());
            return this;
        }

        public StringValidator Double()
        {
            _inputValidators.Add(new DoubleValidator());
            return this;
        }

        public StringValidator Custom(IInputValidator validator)
        {
            _inputValidators.Add(validator);
            return this;
        }

        public StringValidator If(Predicate<string> predicate)
        {
            _inputValidators.Add(new PredicateValidator(predicate));
            return this;
        }

        public bool Validate(string input) =>
            _inputValidators.All(v => v.Validate(input));

        public void EnsureValid(string input)
        {
            var exceptions = _inputValidators
                .Where(v => !v.Validate(input))
                .Select(v => new MenuInputException(v.ErrorMessage));

            if (exceptions.Any())
                throw new AggregateException(exceptions);
        }
    }

    public interface IInputValidator
    {
        bool Validate(string input);

        string ErrorMessage { get; }
    }
}