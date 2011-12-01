/*
 * Copyright (c) 2011 Calvin Rien
 * 
 * Based on the JSON parser by Patrick van Bergen 
 * http://techblog.procurios.nl/k/618/news/view/14605/14863/How-do-I-write-my-own-parser-for-JSON.html
 * 
 * Simplified it so that it doesn't throw exceptions
 * and can be used in Unity iPhone with maximum code stripping.
 *
 * Permission is hereby granted, free of charge, to any person obtaining
 * a copy of this software and associated documentation files (the
 * "Software"), to deal in the Software without restriction, including
 * without limitation the rights to use, copy, modify, merge, publish,
 * distribute, sublicense, and/or sell copies of the Software, and to
 * permit persons to whom the Software is furnished to do so, subject to
 * the following conditions:
 * 
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
 * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
 * IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
 * CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
 * TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
 * SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// This class encodes and decodes JSON strings.
/// Spec. details, see http://www.json.org/
/// 
/// JSON uses Arrays and Objects. These correspond here to the datatypes IList and IDictionary.
/// All numbers are parsed to doubles.
/// </summary>
public static class MiniJSON
{
    const int TOKEN_NONE = 0;
    const int TOKEN_CURLY_OPEN = 1;
    const int TOKEN_CURLY_CLOSE = 2;
    const int TOKEN_SQUARED_OPEN = 3;
    const int TOKEN_SQUARED_CLOSE = 4;
    const int TOKEN_COLON = 5;
    const int TOKEN_COMMA = 6;
    const int TOKEN_STRING = 7;
    const int TOKEN_NUMBER = 8;
    const int TOKEN_TRUE = 9;
    const int TOKEN_FALSE = 10;
    const int TOKEN_NULL = 11;

    const int BUILDER_CAPACITY = 2000;

    /// <summary>
    /// On decoding, this value holds the position at which the parse failed (-1 = no error).
    /// </summary>
    static int lastErrorIndex = -1;
    static string lastDecode;

    /// <summary>
    /// Parses the string json into a value
    /// </summary>
    /// <param name="json">A JSON string.</param>
    /// <returns>An List&lt;object&gt;, a Dictionary&lt;string, object&gt;, a double, a string, null, true, or false</returns>
    public static object Decode(string json)
    {
        // save the string for debug information
        lastDecode = json;

        if (json != null) {
            char[] charArray = json.ToCharArray();
            int index = 0;
            bool success = true;
            object value = ParseValue(charArray, ref index, ref success);
            if (success) {
                lastErrorIndex = -1;
            } else {
                lastErrorIndex = index;
            }
            return value;
        } else {
            return null;
        }
    }

    /// <summary>
    /// Converts a IDictionary / IList object or a simple type (string, int, etc.) into a JSON string
    /// </summary>
    /// <param name="json">A Dictionary&lt;string, object&gt; / List&lt;object&gt;</param>
    /// <returns>A JSON encoded string, or null if object 'json' is not serializable</returns>
    public static string Encode(object json)
    {
        StringBuilder builder = new StringBuilder(BUILDER_CAPACITY);
        bool success = SerializeValue(json, builder);
        return (success ? builder.ToString() : null);
    }

    /// <summary>
    /// On decoding, this function returns the position at which the parse failed (-1 = no error).
    /// </summary>
    /// <returns></returns>
    public static bool LastDecodeSuccessful()
    {
        return (lastErrorIndex == -1);
    }

    /// <summary>
    /// On decoding, this function returns the position at which the parse failed (-1 = no error).
    /// </summary>
    /// <returns></returns>
    public static int GetLastErrorIndex()
    {
        return lastErrorIndex;
    }

    /// <summary>
    /// If a decoding error occurred, this function returns a piece of the JSON string
    /// at which the error took place. To ease debugging.
    /// </summary>
    /// <returns></returns>
    public static string GetLastErrorSnippet()
    {
        if (lastErrorIndex == -1) {
            return "";
        } else {
            int startIndex = lastErrorIndex - 5;
            int endIndex = lastErrorIndex + 15;
            if (startIndex < 0) {
                startIndex = 0;
            }
            if (endIndex >= lastDecode.Length) {
                endIndex = lastDecode.Length - 1;
            }

            return lastDecode.Substring(startIndex, endIndex - startIndex + 1);
        }
    }

    static Dictionary<string, object> ParseObject(char[] json, ref int index)
    {
        Dictionary<string, object> table = new Dictionary<string, object>();
        int token;

        // {
        NextToken(json, ref index);

        while (true) {
            token = LookAhead(json, index);
            if (token == TOKEN_NONE) {
                return null;
            } else if (token == TOKEN_COMMA) {
                NextToken(json, ref index);
            } else if (token == TOKEN_CURLY_CLOSE) {
                NextToken(json, ref index);
                return table;
            } else {

                // name
                string name = ParseString(json, ref index);
                if (name == null) {
                    return null;
                }

                // :
                token = NextToken(json, ref index);
                if (token != TOKEN_COLON) {
                    return null;
                }

                // value
                bool success = true;
                object value = ParseValue(json, ref index, ref success);
                if (!success) {
                    return null;
                }

                table[name] = value;
            }
        }
    }

    static List<object> ParseArray(char[] json, ref int index)
    {
        List<object> array = new List<object>();

        // [
        NextToken(json, ref index);

        while (true) {
            int token = LookAhead(json, index);
            if (token == TOKEN_NONE) {
                return null;
            } else if (token == TOKEN_COMMA) {
                NextToken(json, ref index);
            } else if (token == TOKEN_SQUARED_CLOSE) {
                NextToken(json, ref index);
                break;
            } else {
                bool success = true;
                object value = ParseValue(json, ref index, ref success);
                if (!success) {
                    return null;
                }

                array.Add(value);
            }
        }

        return array;
    }

    static object ParseValue(char[] json, ref int index, ref bool success)
    {
        switch (LookAhead(json, index)) {
        case TOKEN_STRING:
            return ParseString(json, ref index);
        case TOKEN_NUMBER:
            return ParseNumber(json, ref index);
        case TOKEN_CURLY_OPEN:
            return ParseObject(json, ref index);
        case TOKEN_SQUARED_OPEN:
            return ParseArray(json, ref index);
        case TOKEN_TRUE:
            NextToken(json, ref index);
            return true;
        case TOKEN_FALSE:
            NextToken(json, ref index);
            return false;
        case TOKEN_NULL:
            NextToken(json, ref index);
            return null;
        case TOKEN_NONE:
            break;
        }

        success = false;
        return null;
    }

    static string ParseString(char[] json, ref int index)
    {
        StringBuilder s = new StringBuilder();
        char c;

        EatWhitespace(json, ref index);

        c = json[index++];

        bool complete = false;
        while (!complete) {

            if (index == json.Length) {
                break;
            }

            c = json[index++];
            if (c == '"') {
                complete = true;
                break;
            } else if (c == '\\') {

                if (index == json.Length) {
                    break;
                }
                c = json[index++];

                if (c == '"') {
                    s.Append('"');
                } else if (c == '\\') {
                    s.Append('\\');
                } else if (c == '/') {
                    s.Append('/');
                } else if (c == 'b') {
                    s.Append('\b');
                } else if (c == 'f') {
                    s.Append('\f');
                } else if (c == 'n') {
                    s.Append('\n');
                } else if (c == 'r') {
                    s.Append('\r');
                } else if (c == 't') {
                    s.Append('\t');
                } else if (c == 'u') {
                    int remainingLength = json.Length - index;
                    if (remainingLength >= 4) {
                        char[] unicodeCharArray = new char[4];
                        Array.Copy(json, index, unicodeCharArray, 0, 4);

                        // Drop in the HTML markup for the unicode character
                        s.AppendFormat(string.Format("&#x{0};", unicodeCharArray));

                        // skip 4 chars
                        index += 4;
                    } else {
                        break;
                    }
                }
            } else {
                s.Append(c);
            }

        }

        if (!complete) {
            return null;
        }

        return s.ToString();
    }

    static object ParseNumber(char[] json, ref int index)
    {
        EatWhitespace(json, ref index);

        int lastIndex = GetLastIndexOfNumber(json, index);
        int charLength = (lastIndex - index) + 1;
        char[] numberCharArray = new char[charLength];

        Array.Copy(json, index, numberCharArray, 0, charLength);
        index = lastIndex + 1;

		string numberStr = new string(numberCharArray);

		if (numberStr.IndexOf('.') == -1) {
			return long.Parse(numberStr);
		}

        return Double.Parse(numberStr);
    }

    static int GetLastIndexOfNumber(char[] json, int index)
    {
        int lastIndex;
        for (lastIndex = index; lastIndex < json.Length; lastIndex++) {
            if ("0123456789+-.eE".IndexOf(json[lastIndex]) == -1) {
                break;
            }
        }
        return lastIndex - 1;
    }

    static void EatWhitespace(char[] json, ref int index)
    {
        for (; index < json.Length; index++) {
            if (" \t\n\r".IndexOf(json[index]) == -1) {
                break;
            }
        }
    }

    static int LookAhead(char[] json, int index)
    {
        int saveIndex = index;
        return NextToken(json, ref saveIndex);
    }

    static int NextToken(char[] json, ref int index)
    {
        EatWhitespace(json, ref index);

        if (index == json.Length) {
            return TOKEN_NONE;
        }
        
        char c = json[index];
        index++;
        switch (c) {
        case '{':
            return TOKEN_CURLY_OPEN;
        case '}':
            return TOKEN_CURLY_CLOSE;
        case '[':
            return TOKEN_SQUARED_OPEN;
        case ']':
            return TOKEN_SQUARED_CLOSE;
        case ',':
            return TOKEN_COMMA;
        case '"':
            return TOKEN_STRING;
        case '0':
        case '1':
        case '2':
        case '3':
        case '4': 
        case '5':
        case '6':
        case '7':
        case '8':
        case '9':
        case '-': 
            return TOKEN_NUMBER;
        case ':':
            return TOKEN_COLON;
        }
        index--;

        int remainingLength = json.Length - index;

        if (remainingLength >= 5) {
            // false
            if (json[index] == 'f' &&
                json[index + 1] == 'a' &&
                json[index + 2] == 'l' &&
                json[index + 3] == 's' &&
                json[index + 4] == 'e') {
                index += 5;
                return TOKEN_FALSE;
            }
        }

        if (remainingLength >= 4) {
            // true
            if (json[index] == 't' &&
                json[index + 1] == 'r' &&
                json[index + 2] == 'u' &&
                json[index + 3] == 'e') {
                index += 4;
                return TOKEN_TRUE;
            }

            // null
            if (json[index] == 'n' &&
                json[index + 1] == 'u' &&
                json[index + 2] == 'l' &&
                json[index + 3] == 'l') {
                index += 4;
                return TOKEN_NULL;
            }
        }

        return TOKEN_NONE;
    }

    static bool SerializeObject(IDictionary anObject, StringBuilder builder)
    {
        bool first = true;

        builder.Append('{');

        foreach (object e in anObject.Keys) {
            if (!first) {
                builder.Append(',');
            }
            
            SerializeString(e.ToString(), builder);
            builder.Append(':');

            if (!SerializeValue(anObject[e], builder)) {
                return false;
            }
            
            first = false;
        }

        builder.Append('}');
        return true;
    }

    static bool SerializeArray(IList anArray, StringBuilder builder)
    {
        builder.Append('[');

        bool first = true;

        foreach (object obj in anArray) {
            if (!first) {
                builder.Append(',');
            }

            if (!SerializeValue(obj, builder)) {
                return false;
            }

            first = false;
        }

        builder.Append(']');
        return true;
    }

    static bool SerializeValue(object value, StringBuilder builder)
    {
        if (value == null) {
            builder.Append("null");
        } else if (value.GetType().IsArray) {
            SerializeArray((IList) value, builder);
        } else if (value is string) {
            SerializeString((string)value, builder);
        } else if (value is Char) {         
            SerializeString(Convert.ToString((char)value), builder);
        } else if (value is IDictionary) {
            SerializeObject((IDictionary)value, builder);
        } else if (value is IList) {
            SerializeArray((IList)value, builder);
        } else if (value is bool) {
            builder.Append((bool)value ? "true" : "false");
        } else if (value.GetType().IsPrimitive) {
            SerializeNumber(Convert.ToDouble(value), builder);
        } else {
            return false;
        }
        return true;
    }

    static void SerializeString(string aString, StringBuilder builder)
    {
        builder.Append('\"');

        char[] charArray = aString.ToCharArray();
        foreach (var c in charArray) {
            if (c == '"') {
                builder.Append("\\\"");
            } else if (c == '\\') {
                builder.Append("\\\\");
            } else if (c == '\b') {
                builder.Append("\\b");
            } else if (c == '\f') {
                builder.Append("\\f");
            } else if (c == '\n') {
                builder.Append("\\n");
            } else if (c == '\r') {
                builder.Append("\\r");
            } else if (c == '\t') {
                builder.Append("\\t");
            } else {
                int codepoint = Convert.ToInt32(c);
                if ((codepoint >= 32) && (codepoint <= 126)) {
                    builder.Append(c);
                } else {
                    builder.Append("\\u" + Convert.ToString(codepoint, 16).PadLeft(4, '0'));
                }
            }
        }

        builder.Append('\"');
    }

    static void SerializeNumber(double number, StringBuilder builder)
    {
        builder.Append(number.ToString());
    }
}