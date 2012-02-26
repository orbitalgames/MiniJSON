/*
 * Copyright (c) 2012 Calvin Rien
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
using System.IO;
using System.Text;

namespace MiniJSON
{
    // Example usage:
    //
    //  using UnityEngine;
    //  using System.Collections;
    //  using System.Collections.Generic;
    //  using MiniJSON;
    //
    //  public class MiniJSONTest : MonoBehaviour {
    //      void Start () {
    //          var jsonString = "{ \"array\": [1.44,2,3], " +
    //                          "\"object\": {\"key1\":\"value1\", \"key2\":256}, " +
    //                          "\"string\": \"The quick brown fox \\\"jumps\\\" over the lazy dog \", " +
    //                          "\"unicode\": \"\\u3041 Men\u00fa sesi\u00f3n\", " +
    //                          "\"int\": 65536, " +
    //                          "\"float\": 3.1415926, " +
    //                          "\"bool\": true, " +
    //                          "\"null\": null }";
    //
    //          var dict = Json.Deserialize(jsonString) as Dictionary<string,object>;
    //
    //          Debug.Log("deserialized: " + dict.GetType());
    //          Debug.Log("dict['array'][0]: " + ((List<object>) dict["array"])[0]);
    //          Debug.Log("dict['string']: " + (string) dict["string"]);
    //          Debug.Log("dict['float']: " + (double) dict["float"]); // floats come out as doubles
    //          Debug.Log("dict['int']: " + (long) dict["int"]); // ints come out as longs
    //          Debug.Log("dict['unicode']: " + (string) dict["unicode"]);
    //
    //          var str = Json.Serialize(dict);
    //
    //          Debug.Log("serialized: " + str);
    //      }
    //  }

    /// <summary>
    /// This class encodes and decodes JSON strings.
    /// Spec. details, see http://www.json.org/
    /// 
    /// JSON uses Arrays and Objects. These correspond here to the datatypes IList and IDictionary.
    /// All numbers are parsed to doubles.
    /// </summary>
    public class Json {
        /// <summary>
        /// Parses the string json into a value
        /// </summary>
        /// <param name="json">A JSON string.</param>
        /// <returns>An List&lt;object&gt;, a Dictionary&lt;string, object&gt;, a double, an integer,a string, null, true, or false</returns>
        public static object Deserialize(string json) {
            // save the string for debug information
            if (json == null)
                return null;
                
            var parser = new Parser(json);
            return parser.Parse();
        }
        
        class Parser {
            StringReader json;

            enum TOKEN {
                NONE, 
                CURLY_OPEN,
                CURLY_CLOSE,
                SQUARED_OPEN,
                SQUARED_CLOSE,
                COLON,
                COMMA,
                STRING,
                NUMBER,
                TRUE,
                FALSE,
                NULL
            };          
            
            public Parser(string jsonData) {
                this.json = new StringReader(jsonData);
            }
            
            public object Parse() {
                return ParseValue();
            }
            
            Dictionary<string, object> ParseObject() {
                Dictionary<string, object> table = new Dictionary<string, object>();
                TOKEN token;

				// ditch opening brace
				json.Read();

                // {
                while (true) {
                    token = NextToken();

					switch (token) {
					case TOKEN.NONE:
						return null;
					case TOKEN.COMMA:
						continue;
					case TOKEN.CURLY_CLOSE:
						return table;
					default:
                        // name
                        string name = ParseString();
                        if (name == null) {
                            return null;
                        }

                        // :
                        token = NextToken();
                        if (token != TOKEN.COLON) {
                            return null;
                        }
						// ditch the colon
						json.Read();

                        // value
                        table[name] = ParseValue();
						break;
					}
                }
            }

            List<object> ParseArray() {
                List<object> array = new List<object>();
                TOKEN token;

				// ditch opening bracket
				json.Read();

                // [
                while (true) {
                    token = NextToken();
                    if (token == TOKEN.NONE) {
                        return null;
                    } else if (token == TOKEN.COMMA) {
                        continue;
                    } else if (token == TOKEN.SQUARED_CLOSE) {
                        break;
                    } else {
                        object value = ParseValue();

                        array.Add(value);
                    }
                }

                return array;
            }
                        
            object ParseValue() {
                switch (NextToken()) {
                case TOKEN.STRING:
                    return ParseString();
                case TOKEN.NUMBER:
                    return ParseNumber();
                case TOKEN.CURLY_OPEN:
                    return ParseObject();
                case TOKEN.SQUARED_OPEN:
                    return ParseArray();
                case TOKEN.TRUE:
                    return true;
                case TOKEN.FALSE:
                    return false;
                case TOKEN.NULL:
                    return null;
                default:
                    return null;
                }
            }

            string ParseString() {
                StringBuilder s = new StringBuilder();
                char c;

				// ditch opening quote
				json.Read();

                bool complete = false;
                while (true) {

                    if (json.Peek() == -1) {
                        break;
                    }

                    c = ReadChar();
                    if (c == '"') {
                        complete = true;
                        break;
                    } else if (c == '\\') {
                        if (json.Peek() == -1) {
                            break;
                        }

                        c = ReadChar();

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
                            var hex = new StringBuilder();

							for (int i=0; i< 4;i++) {
								hex.Append(ReadChar());
							}

                            s.Append((char) Convert.ToInt32(hex.ToString(), 16));
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

            object ParseNumber() {
                string number = NextWord();

                if (number.IndexOf('.') == -1) {
                    return Int64.Parse(number);
                }

                return Double.Parse(number);
            }

            void EatWhitespace() {
                while (" \t\n\r".IndexOf(PeekChar()) != -1) {
                    json.Read();

                    if (json.Peek() == -1)
                        break;
                }
            }

			char PeekChar() {
				return Convert.ToChar(json.Peek());
			}

			char ReadChar() {
				return Convert.ToChar(json.Read());
			}

            string NextWord() {
                StringBuilder word = new StringBuilder();

                while (" \t\n\r{}[],:\"".IndexOf(PeekChar()) == -1) {
                    word.Append(ReadChar());

                    if (json.Peek() == -1)
                        break;
                }

                return word.ToString();
            }

            TOKEN NextToken() {
                EatWhitespace();

                if (json.Peek() == -1) {
                    return TOKEN.NONE;
                }
        
                char c = PeekChar();
                switch (c) {
                case '{':
                    return TOKEN.CURLY_OPEN;
                case '}':
					json.Read();
                    return TOKEN.CURLY_CLOSE;
                case '[':
                    return TOKEN.SQUARED_OPEN;
                case ']':
					json.Read();
                    return TOKEN.SQUARED_CLOSE;
                case ',':
					json.Read();
                    return TOKEN.COMMA;
                case '"':
                    return TOKEN.STRING;
                case ':':
                    return TOKEN.COLON;
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
                    return TOKEN.NUMBER;
                }

                string word = NextWord();

                switch (word) {
                case "false":
                    return TOKEN.FALSE;
                case "true":
                    return TOKEN.TRUE;
                case "null":
                    return TOKEN.NULL;
                }

                return TOKEN.NONE;
            }
        }

        /// <summary>
        /// Converts a IDictionary / IList object or a simple type (string, int, etc.) into a JSON string
        /// </summary>
        /// <param name="json">A Dictionary&lt;string, object&gt; / List&lt;object&gt;</param>
        /// <returns>A JSON encoded string, or null if object 'json' is not serializable</returns>
        public static string Serialize(object obj) {
            var serializer = new Serializer(obj);
            
            return serializer.Serialize();
        }
        
        class Serializer {
            StringBuilder builder;
            object obj;
            
            public Serializer(object obj) {
                this.obj = obj;
                builder = new StringBuilder();
            }
            
            public string Serialize() {
                SerializeValue(obj);
                
                return builder.ToString();
            }

            void SerializeValue(object value) {
                if (value == null) {
                    builder.Append("null");
                } else if (value is IDictionary) {
                    SerializeObject((IDictionary)value);
                } else if (value is IList) {
                    SerializeArray((IList)value);
                } else if (value is string) {
                    SerializeString((string)value);
                } else if (value is Char) { 
                    SerializeString(((char)value).ToString());
                } else if (value is bool) {
                    builder.Append((bool)value ? "true" : "false");
                } else {
					SerializeOther(value);
                }
            }
            
            void SerializeObject(IDictionary obj) {
                bool first = true;

                builder.Append('{');

                foreach (object e in obj.Keys) {
                    if (!first) {
                        builder.Append(',');
                    }
            
                    SerializeString(e.ToString());
                    builder.Append(':');

                    SerializeValue(obj[e]);
            
                    first = false;
                }

                builder.Append('}');
            }

            void SerializeArray(IList anArray) {
                builder.Append('[');

                bool first = true;

                foreach (object obj in anArray) {
                    if (!first) {
                        builder.Append(',');
                    }

                    SerializeValue(obj);

                    first = false;
                }

                builder.Append(']');
            }

            void SerializeString(string str) {
                builder.Append('\"');

                char[] charArray = str.ToCharArray();
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

			void SerializeOther(object value) {
			    if (value is float
                    || value is int
                    || value is uint
                    || value is long
                    || value is double
                    || value is sbyte
					|| value is byte
					|| value is short
					|| value is ushort
					|| value is ulong
					|| value is decimal) {
					builder.Append(value.ToString());
				}
				else {
					SerializeString(value.ToString());
				}
			}
        }
    }
}