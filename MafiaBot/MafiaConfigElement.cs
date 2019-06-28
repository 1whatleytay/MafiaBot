using System;
using System.Text;

namespace MafiaBot {
    public class MafiaConfigElement {
        private readonly double _number;
        private readonly bool _isPercentage;
        private readonly bool _roundUp;

        public int GetCount(int numberOfPlayers) {
            if (_isPercentage) {
                var number = _number * numberOfPlayers;
                
                if (_roundUp)
                    return (int) Math.Ceiling(number);
                
                return (int) Math.Floor(number);
            }

            return (int)_number;
        }

        public string GetDescription() {
            var postfix = _isPercentage ? "%" : "";
            var end = _isPercentage ? _roundUp ? " rounded up" : " rounded down" : "";
            var number = _isPercentage ? $"{_number * 100:N2}" : $"{(int) _number}";
            return $"{number}{postfix}{end}";
        }

        public MafiaConfigElement(double number, bool isPercentage = false, bool roundUp = false) {
            _number = number;
            _isPercentage = isPercentage;
            _roundUp = roundUp;
        }
        
        public MafiaConfigElement(string number) {
            var index = 0;

            if (number[index] == '+' || number[index] == '-') {
                _roundUp = number[index] == '+';
                index++;
            }

            var builder = new StringBuilder();
            while (index < number.Length && (char.IsDigit(number[index]) || number[index] == '.')) {
                builder.Append(number[index]);
                index++;
            }
            _number = double.Parse(builder.ToString());

            if (index < number.Length && number[index] == '%') {
                _number /= 100;
                _isPercentage = true;
                index++;
            }

            if (index < number.Length) Console.WriteLine($"Extra text at the end of {number}.");
        }
    }
}