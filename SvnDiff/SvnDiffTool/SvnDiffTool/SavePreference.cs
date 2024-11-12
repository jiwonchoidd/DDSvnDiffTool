using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SvnDiffTool
{
    public static class SavePreference
    {
        static string fileName = "SavePreference.ddchoi";

        public static void LoadUserPreference()
        {
            Preferences = new Dictionary<string, object>();
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, "");
            }
            else
            {
                string Context = File.ReadAllText(filePath);

                string[] elements = Context.Split(']');

                for (int i = 0; i < elements.Length - 1; i++)
                {
                    // 요소를 ','를 기준으로 분할하여 각 부분을 추출
                    string[] parts = elements[i].Split(',');

                    string key = parts[0];
                    string valueString = parts[1];
                    string type = parts[2].Trim();

                    object value = ParseValue(valueString, type);

                    Preferences.Add(key, value);
                }
            }
        }
        public static void SaveUserPreference()
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);

            string Context = "";
            foreach(var Preference in Preferences)
            {
                Context += Preference.Key;
                Context += ",";
                Context += Preference.Value.ToString();
                Context += ",";
                Context += Preference.Value.GetType();
                Context += "]";
            }

            File.WriteAllText(filePath, Context);
        }

        public static object? GetValue(string _Key)
        {
            if(Preferences.ContainsKey(_Key))
            {
                return Preferences[_Key];
            }
            return null;
        }

        public static void SetValue<T>(string _Key, T _Value)
        {
            Preferences[_Key] = _Value;
        }

        static object ParseValue(string _valueString, string _type)
        {
            switch (_type)
            {
                case "System.Int32":
                    return int.Parse(_valueString);
                case "System.Boolean":
                    return bool.Parse(_valueString);
                case "System.String":
                    return _valueString;
                // 추가 유형에 대한 처리도 추가 가능
                default:
                    throw new ArgumentException("Unsupported type: " + _type);
            }
        }

        static Dictionary<string, object> Preferences;
    }
}
