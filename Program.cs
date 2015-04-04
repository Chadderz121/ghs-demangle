using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ghs_demangle
{
    class Program
    {
        /* all the prefixes which behave as templates */
        static string[] templatePrefixes = new string[] { "tm", "ps", "pt" /* XXX from libiberty cplus-dem.c */ };
        static Dictionary<string, string> baseNames;
        static Dictionary<char, string> baseTypes;
        static Dictionary<char, string> typePrefixes;
        static Dictionary<char, string> typeSuffixes;

        static Program()
        {
            baseNames = new Dictionary<string, string>();
            baseNames.Add("__vtbl", " virtual table");
            baseNames.Add("__ct", "#");
            baseNames.Add("__dt", "~#");
            baseNames.Add("__as", "operator=");
            baseNames.Add("__eq", "operator==");
            baseNames.Add("__ne", "operator!=");
            baseNames.Add("__gt", "operator>");
            baseNames.Add("__lt", "operator<");
            baseNames.Add("__ge", "operator>=");
            baseNames.Add("__le", "operator<=");
            baseNames.Add("__pp", "operator++");
            baseNames.Add("__pl", "operator+");
            baseNames.Add("__apl", "operator+=");
            baseNames.Add("__mi", "operator-");
            baseNames.Add("__ami", "operator-=");
            baseNames.Add("__ml", "operator*");
            baseNames.Add("__amu", "operator*=");
            baseNames.Add("__dv", "operator/");
            /* XXX below baseNames have not been seen - guess from libiberty cplus-dem.c */
            baseNames.Add("__adv", "operator/=");
            baseNames.Add("__nw", "operator new");
            baseNames.Add("__dl", "operator delete");
            baseNames.Add("__vn", "operator new[]");
            baseNames.Add("__vd", "operator delete[]");
            baseNames.Add("__md", "operator%");
            baseNames.Add("__amd", "operator%=");
            baseNames.Add("__mm", "operator--");
            baseNames.Add("__aa", "operator&&");
            baseNames.Add("__oo", "operator||");
            baseNames.Add("__or", "operator|");
            baseNames.Add("__aor", "operator|=");
            baseNames.Add("__er", "operator^");
            baseNames.Add("__aer", "operator^=");
            baseNames.Add("__ad", "operator&");
            baseNames.Add("__aad", "operator&=");
            baseNames.Add("__co", "operator~");
            baseNames.Add("__cl", "operator()");
            baseNames.Add("__ls", "operator<<");
            baseNames.Add("__als", "operator<<=");
            baseNames.Add("__rs", "operator>>");
            baseNames.Add("__ars", "operator>>=");
            baseNames.Add("__rf", "operator->");
            baseNames.Add("__vc", "operator[]");
            baseTypes = new Dictionary<char,string>();
            baseTypes.Add('v', "void");
            baseTypes.Add('i', "int");
            baseTypes.Add('s', "short");
            baseTypes.Add('c', "char");
            baseTypes.Add('w', "wchar_t");
            baseTypes.Add('b', "bool");
            baseTypes.Add('f', "float");
            baseTypes.Add('d', "double");
            baseTypes.Add('l', "long");
            baseTypes.Add('L', "long long");
            baseTypes.Add('e', "...");
            /* XXX below baseTypes have not been seen - guess from libiberty cplus-dem.c */
            baseTypes.Add('r', "long double");
            typePrefixes = new Dictionary<char, string>();
            typePrefixes.Add('U', "unsigned");
            typePrefixes.Add('S', "signed");
            /* XXX below typePrefixes have not been seen - guess from libiberty cplus-dem.c */
            typePrefixes.Add('J', "__complex");
            typeSuffixes = new Dictionary<char, string>();
            typeSuffixes.Add('P', "*");
            typeSuffixes.Add('R', "&");
            typeSuffixes.Add('C', "const");
            typeSuffixes.Add('V', "volatile"); /* XXX this is a guess! */
            /* XXX below typeSuffixes have not been seen - guess from libiberty cplus-dem.c */
            typeSuffixes.Add('u', "restrict");
        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return;
            }

            int currentArg;
            for (currentArg = 0; currentArg < args.Length; currentArg++)
            {
                if (args[currentArg].Equals("--help", StringComparison.Ordinal))
                {
                    PrintHelp();
                    return;
                }
                else if (args[currentArg].Equals("--version", StringComparison.Ordinal))
                {
                    PrintVersion();
                    return;
                }
                else if (args[currentArg].Equals("--", StringComparison.Ordinal))
                {
                    currentArg++;
                    break;
                }
                else if (args[currentArg].Equals("-", StringComparison.Ordinal))
                    break;
                else if (args[currentArg].StartsWith("-", StringComparison.Ordinal))
                {
                    Console.Error.WriteLine("Invalid argument \"" + args[currentArg] + "\".");
                    PrintHelp();
                    return;
                }
                else
                    break;
            }

            for (; currentArg < args.Length; currentArg++)
            {
                bool stdin = args[currentArg].Equals("-", StringComparison.Ordinal);
                using (StreamReader r = stdin ? new StreamReader(Console.OpenStandardInput()) : new StreamReader(args[currentArg]))
                    DemangleAll(r, stdin ? "stdin" : args[currentArg]);
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine("ghs-demangle [OPTION].. [--] [FILE]..");
            Console.WriteLine("Demangles all symbols in FILE(s) to standard output.");
            Console.WriteLine("Error messages are displayed on standard error.");
            Console.WriteLine("Use \"-\" as file for standard input.");
            Console.WriteLine("");
            Console.WriteLine("  --help     Display this help message and exit.");
            Console.WriteLine("  --version  Display version message and exit.");
        }

        private static void PrintVersion()
        {
            Console.WriteLine(
                System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + " v" +
                System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3));
            Console.WriteLine("by Alex Chadwick");
            Console.WriteLine("");
            Console.WriteLine(
                ((System.Reflection.AssemblyCopyrightAttribute)Attribute.GetCustomAttribute(System.Reflection.Assembly.GetExecutingAssembly(), typeof(System.Reflection.AssemblyCopyrightAttribute))).Copyright);
            Console.WriteLine("This is free software; see the source for copying conditions.  There is NO");
            Console.WriteLine("warranty; not even for MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.");
        }

        static void DemangleAll(StreamReader r, string fileName)
        {
            int line = 0;
            string name;

            while (!r.EndOfStream)
            {
                line++;
                name = r.ReadLine();
                try
                {
                    if (!string.IsNullOrEmpty(name))
                        name = Demangle(name);
                }
                catch (InvalidDataException e)
                {
                    Console.Error.WriteLine(fileName + ":" + line.ToString(CultureInfo.InvariantCulture) + ": " + e.Message);
                }
                Console.WriteLine(name);
            }
        }

        /* e.g. "h__Fi" => "h(int)" */
        static string Demangle(string name)
        {
            name = Decompress(name);

            /* This demangle method has basically turned into a hand-written
             * LL(1) recursive descent parser. */

            string mangle;
            string baseName = ReadBaseName(name, out mangle);

            /* TODO this may not be right - see S below Q */
            /* h__S__Q1_3clsFi => static cls::h(int) */
            string declStatic;
            if (mangle.StartsWith("S__", StringComparison.Ordinal))
            {
                declStatic = "static ";
                mangle = mangle.Substring(3);
            }
            else
                declStatic = "";

            string declNameSpace, declClass;
            if (mangle.StartsWith("Q", StringComparison.Ordinal))
            {
                declNameSpace = ReadNameSpace(mangle, out mangle);

                int last = declNameSpace.LastIndexOf("::", StringComparison.Ordinal);
                if (last != -1)
                    declClass = declNameSpace.Substring(last + 2);
                else
                    declClass = declNameSpace;

                declNameSpace += "::";
            }
            else if (mangle.Length > 0 && char.IsDigit(mangle[0]))
            {
                declClass = ReadString(mangle, out mangle);
                declNameSpace = declClass + "::";
            }
            else
            {
                declNameSpace = "";
                declClass = "";
            }

            baseName = baseName.Replace("#", declClass);

            /* static */
            if (mangle.StartsWith("S", StringComparison.Ordinal))
            {
                declStatic = "static ";
                mangle = mangle.Substring(1);
            }

            string declConst;
            if (mangle.StartsWith("C", StringComparison.Ordinal))
            {
                declConst = " const";
                mangle = mangle.Substring(1);
            }
            else
                declConst = "";

            string declType;
            if (mangle.StartsWith("F", StringComparison.Ordinal))
                declType = ReadType(null, mangle, out mangle);
            else
                declType = "#";

            /* XXX bit of a hack - some names I see seem to end with _<number> */
            int end;
            if (mangle.StartsWith("_", StringComparison.Ordinal) &&
                int.TryParse(mangle.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out end))
            {
                baseName += "_" + end.ToString(CultureInfo.InvariantCulture);
                mangle = "";
            }

            if (mangle.Length > 0)
                throw new InvalidDataException("Unknown modifier: \"" + mangle[0] + "\".");

            return (declStatic + declType.Replace("(#)", " " + declNameSpace + baseName).Replace("#", declNameSpace + baseName) + declConst).Replace("::" + baseNames["__vtbl"], baseNames["__vtbl"]);
        }

        /* e.g. "__CPR51__method__19ReallyLongClassNameFJ6J" => "method__19ReallyLongClassNameF19ReallyLongClassName" */
        static string Decompress(string name)
        {
            if (!name.StartsWith("__CPR", StringComparison.Ordinal))
                return name;

            name = name.Substring(5);

            int decompressedLen = ReadInt(name, out name);

            if (name.Length == 0)
                throw new InvalidDataException("Unexpected end of string. Expected compressed symbol name.");
            if (!name.StartsWith("__", StringComparison.Ordinal))
                throw new InvalidDataException("Unexpected character(s) after compression len: \"" + name[0] + "\". Expected \"__\".");
            name = name.Substring(2);

            string result = "";
            int index = 0;

            /* find all instances of J<num>J */
            while (true)
            {
                int start = name.IndexOf('J', index);

                if (start != -1)
                {
                    result += name.Substring(index, start - index);

                    int end = name.IndexOf('J', start + 1);

                    if (end != -1)
                    {
                        bool valid = true;

                        /* check all characters between Js are digits */
                        for (int i = start + 1; i < end; i++)
                            if (!char.IsNumber(name[i]))
                            {
                                valid = false;
                                break;
                            }

                        if (valid)
                        {
                            int loc = int.Parse(name.Substring(start + 1, end - start - 1), CultureInfo.InvariantCulture);

                            string tmp;
                            int len = ReadInt(result.Substring(loc), out tmp);

                            if (len == 0 || len > tmp.Length)
                                throw new InvalidDataException("Bad string length \"" + len.ToString(CultureInfo.InvariantCulture) + "\".");

                            result += len.ToString(CultureInfo.InvariantCulture) + tmp.Substring(0, len);
                            index = end + 1;
                        }
                        else
                        {
                            result += name.Substring(start, 1);
                            index = start + 1;
                        }
                    }
                    else
                    {
                        result += name.Substring(start, 1);
                        index = start + 1;
                    }
                }
                else
                {
                    result += name.Substring(index);
                    break;
                }
            }

            if (result.Length != decompressedLen)
                throw new InvalidDataException("Bad decompression length length \"" + decompressedLen.ToString(CultureInfo.InvariantCulture) + "\". Expected \"" + result.Length.ToString(CultureInfo.InvariantCulture) + "\".");

            return result;
        }

        /* function leaves a "#" character where the class name ought to go */
        /* e.g. "h__Fi" => ("h", "Fi") */
        /* e.g. "__ct__tm_2_w__Fi" => ("#<wchar_t>", "Fi") */
        static string ReadBaseName(string name, out string remainder)
        {
            string opName;
            int mstart;

            if (name.Length == 0)
                throw new InvalidDataException("Unexpected end of string. Expected a name.");

            if (name.StartsWith("__op", StringComparison.Ordinal))
            {
                /* a cast operator */
                string type = ReadType(null, name.Substring(4), out name).Replace("#", "");
                opName = "operator " + type;
                name = "#" + name;
            }
            else
                opName = "";

            mstart = name.IndexOf("__", 1, StringComparison.Ordinal);

            /* check for something like "h___Fi" => "h_" */
            if (mstart != -1 && name.Substring(mstart).StartsWith("___", StringComparison.Ordinal))
                mstart++;

            /* not a special symbol name! */
            if (mstart == -1)
            {
                remainder = "";
                return name;
            }

            /* something more interesting! */
            remainder = name.Substring(mstart + 2);
            name = name.Substring(0, mstart);

            /* check for "__ct__7MyClass" */
            if (baseNames.ContainsKey(name))
                name = baseNames[name];
            else if (name.Equals("#", StringComparison.Ordinal))
                name = opName;

            while (StartsWithAny(remainder, templatePrefixes, StringComparison.Ordinal))
            {
                /* format of remainder should be <type>__<len>_<arg> */
                int lstart = remainder.IndexOf("__", StringComparison.Ordinal);

                if (lstart == -1)
                    throw new InvalidDataException("Bad template argument: \"" + remainder + "\".");

                /* shift across the template type */
                name += "__" + remainder.Substring(0, lstart);
                remainder = remainder.Substring(lstart + 2);

                int len = ReadInt(remainder, out remainder);

                if (len == 0 || len > remainder.Length)
                    throw new InvalidDataException("Bad template argument length: \"" + len.ToString(CultureInfo.InvariantCulture) + "\".");

                /* shift across the len and arg */
                name += "__" + len.ToString(CultureInfo.InvariantCulture) + remainder.Substring(0, len);
                remainder = remainder.Substring(len);

                /* check if we've hit the end */
                if (remainder.Length == 0)
                    return name;

                /* should be immediately followed with __ */
                if (!remainder.StartsWith("__", StringComparison.Ordinal))
                    throw new InvalidDataException("Unexpected character(s) after template: \"" + remainder[0] + "\". Expected \"__\".");
                remainder = remainder.Substring(2);
            }

            return DemangleTemplate(name);
        }

        /* e.g. "vvi_v" => ("void, void, int", "_v") */
        private static string ReadArguments(string name, out string remainder)
        {
            string result = "";
            List<string> args = new List<string>();

            remainder = name;

            while (remainder.Length > 0 && !remainder.StartsWith("_", StringComparison.Ordinal))
            {
                if (args.Count > 0)
                    result += ", ";

                string t = ReadType(args, remainder, out remainder);
                result += t.Replace("#", "");
                args.Add(t);
            }

            return result;
        }

        /* e.g. "vX1xi_v" => ("Z1 = void, Z2 = x, Z3 = int", "_v") */
        private static string ReadTemplateArguments(string name, out string remainder)
        {
            string result = "";
            List<string> args = new List<string>();

            remainder = name;

            while (remainder.Length > 0 && !remainder.StartsWith("_", StringComparison.Ordinal))
            {
                if (args.Count > 0)
                    result += ", ";

                string type, val;

                if (remainder.StartsWith("X", StringComparison.Ordinal))
                {
                    /* X arguments represent named values */

                    remainder = remainder.Substring(1);
                    if (remainder.Length == 0)
                        throw new InvalidDataException("Unexpected end of string. Expected a type.");

                    if (char.IsDigit(remainder[0]))
                    {
                        /* arbitrary string */
                        type = "#";
                        val = ReadString(remainder, out remainder);
                    }
                    else
                    {
                        /* <type><encoding> */
                        type = ReadType(args, remainder, out remainder).Replace("#", " #");

                        if (remainder.StartsWith("L", StringComparison.Ordinal))
                        {
                            /* _<len>_<val> */
                            remainder = remainder.Substring(1);
                            if (remainder.Length == 0)
                                throw new InvalidDataException("Unexpected end of string. Expected \"_\".");
                            if (!remainder.StartsWith("_", StringComparison.Ordinal))
                                throw new InvalidDataException("Unexpected character after template parameter encoding \"" + remainder[0] + "\". Expected \"_\".");

                            int len = ReadInt(remainder.Substring(1), out remainder);

                            if (len == 0 || len > remainder.Length + 1)
                                throw new InvalidDataException("Bad template parameter length: \"" + len.ToString(CultureInfo.InvariantCulture) + "\".");
                            if (!remainder.StartsWith("_"))
                                throw new InvalidDataException("Unexpected character after template parameter length \"" + remainder[0] + "\". Expected \"_\".");

                            remainder = remainder.Substring(1);
                            val = remainder.Substring(0, len);
                            remainder = remainder.Substring(len);
                        }
                        else
                            throw new InvalidDataException("Unknown template parameter encoding: \"" + remainder[0] + "\".");
                    }
                }
                else
                {
                    val = ReadType(args, remainder, out remainder).Replace("#", "");
                    type = "class #";
                }

                /* TODO - the Z notation is ugly - we should resolve args? */
                result += type.Replace("#", "Z" + (args.Count + 1).ToString(CultureInfo.InvariantCulture)) + " = " + val;
                args.Add(val);
            }

            return result;
        }

        /* function leaves a "#" character where the variable name ought to go */
        /* e.g. "iv" => ("int#", "v") */
        /* e.g. "CPFPv_PFv_vPRv" => ("void (*(* const#)(void *))(void)", "PRv") */
        static string ReadType(IList<string> args, string name, out string remainder)
        {
            if (name.Length == 0)
                throw new InvalidDataException("Unexpected end of string. Expected a type.");

            /* e.g. "i" => "int#" */
            if (baseTypes.ContainsKey(name[0]))
            {
                remainder = name.Substring(1);
                return baseTypes[name[0]] + "#";
            }
            /* e.g. "Q2_3std4move__tm__2_w" => "std::move<wchar_t>#" */
            else if (name.StartsWith("Q", StringComparison.Ordinal))
                return ReadNameSpace(name, out remainder) + "#";
            /* e.g. "8MyStruct" => "MyStruct#" */
            else if (char.IsDigit(name[0]))
                return ReadString(name, out remainder) + "#";
            /* e.g. "ui" => "unsigned int#" */
            else if (typePrefixes.ContainsKey(name[0]))
                return typePrefixes[name[0]] + " " + ReadType(args, name.Substring(1), out remainder);
            /* e.g. "Pv" => "void *#" */
            else if (typeSuffixes.ContainsKey(name[0]))
                return ReadType(args, name.Substring(1), out remainder).Replace("#", " " + typeSuffixes[name[0]] + "#");
            /* e.g. "Z1Z" => "Z1#" */
            else if (name.StartsWith("Z"))
            {
                int end = name.IndexOf("Z", 1, StringComparison.Ordinal);

                if (end == -1)
                    throw new InvalidDataException("Unexpected end of string. Expected \"Z\".");

                remainder = name.Substring(end + 1);
                return name.Substring(0, end) + "#";
            }
            /* e.g. "A2_i" => "int#[2]" */
            else if (name.StartsWith("A", StringComparison.Ordinal))
            {
                string len;

                name = name.Substring(1);

                if (name.StartsWith("_Z", StringComparison.Ordinal))
                {
                    int end = name.IndexOf("Z", 2, StringComparison.Ordinal);

                    if (end == -1)
                        throw new InvalidDataException("Unexpected end of string. Expected \"Z\".");

                    len = name.Substring(1, end - 1);
                    name = name.Substring(end + 1);
                }
                else
                    len = ReadInt(name, out name).ToString(CultureInfo.InvariantCulture);

                if (name.Length == 0)
                    throw new InvalidDataException("Unexpected end of string. Expected \"_\".");
                if (!name.StartsWith("_", StringComparison.Ordinal))
                    throw new InvalidDataException("Unexpected character after array length \"" + name[0] + "\". Expected \"_\".");

                return ReadType(args, name.Substring(1), out remainder).Replace("#", "#[" + len + "]");
            }
            /* e.g. "FPv_v" => "void (#)(void *)" */
            else if (name.StartsWith("F", StringComparison.Ordinal))
            {
                string declArgs = ReadArguments(name.Substring(1), out name);

                /* XXX bit of a hack - we're allowed not to have a return type on top level methods, which we detected by the args argument being null. */
                int end;
                if (args == null &&
                    (name.Length == 0 ||
                     (name.StartsWith("_", StringComparison.Ordinal) &&
                      int.TryParse(name.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out end))))
                {
                    remainder = name;
                    return "#(" + declArgs + ")";
                }

                if (name.Length == 0)
                    throw new InvalidDataException("Unexpected end of string. Expected \"_\".");
                if (!name.StartsWith("_", StringComparison.Ordinal))
                    throw new InvalidDataException("Unexpected character after argument declaration \"" + name[0] + "\". Expected \"_\".");

                return ReadType(args, name.Substring(1), out remainder).Replace("#", "(#)(" + declArgs + ")");
            }
            /* T<a> expands to argument <a> */
            else if (name.StartsWith("T", StringComparison.Ordinal))
            {
                if (name.Length < 2)
                    throw new InvalidDataException("Unexpected end of string. Expected \"_\".");
                if (!char.IsDigit(name[1]))
                    throw new InvalidDataException("Unexpected character \"" + name[1] + "\". Expected a digit.");

                int arg = int.Parse(name.Substring(1, 1), CultureInfo.InvariantCulture);

                remainder = name.Substring(2);

                if (args.Count < arg)
                    throw new InvalidDataException("Bad argument number \"" + arg.ToString(CultureInfo.InvariantCulture) + "\".");

                return args[arg - 1];
            }
            /* N<c><a> expands to <c> repetitions of argument <a> */
            else if (name.StartsWith("N", StringComparison.Ordinal))
            {
                if (name.Length < 3)
                    throw new InvalidDataException("Unexpected end of string. Expected \"_\".");
                if (!char.IsDigit(name[1]) || !char.IsDigit(name[2]))
                    throw new InvalidDataException("Unexpected character(s) \"" + name[1] + name[2] + "\". Expected two digits.");

                int count = int.Parse(name.Substring(1, 1), CultureInfo.InvariantCulture);
                int arg = int.Parse(name.Substring(2, 1), CultureInfo.InvariantCulture);

                if (count > 1)
                    remainder = "N" + (count - 1).ToString() + arg.ToString() + name.Substring(3);
                else
                    remainder = name.Substring(3);

                if (args.Count < arg)
                    throw new InvalidDataException("Bad argument number \"" + arg.ToString(CultureInfo.InvariantCulture) + "\".");

                return args[arg - 1];
            }
            else
                throw new InvalidDataException("Unknown type: \"" + name[0] + "\".");
        }

        /* e.g. "Q2_3std13move__tm__2_wv" => ("std::move<wchar_t>", "v") */
        static string ReadNameSpace(string name, out string remainder)
        {
            if (name.Length == 0)
                throw new InvalidDataException("Unexpected end of string. Expected \"Q\".");
            if (!name.StartsWith("Q", StringComparison.Ordinal))
                throw new InvalidDataException("Unexpected character \"" + name[0] + "\". Expected \"Q\".");

            int count = ReadInt(name.Substring(1), out name);

            if (count == 0)
                throw new InvalidDataException("Bad namespace count \"" + count.ToString(CultureInfo.InvariantCulture) + "\".");
            if (name.Length == 0)
                throw new InvalidDataException("Unexpected end of string. Expected \"_\".");
            if (!name.StartsWith("_", StringComparison.Ordinal))
                throw new InvalidDataException("Unexpected character after namespace count \"" + name[0] + "\". Expected \"_\".");

            remainder = name.Substring(1);

            string result = "";
            for (int j = 0; j < count; j++)
            {
                string current;
                if (remainder.StartsWith("Z"))
                {
                    int end = remainder.IndexOf("Z", 1, StringComparison.Ordinal);

                    if (end == -1)
                        throw new InvalidDataException("Unexpected end of string. Expected \"Z\".");

                    current = remainder.Substring(0, end);
                    remainder = name.Substring(end + 1);
                }
                else
                    current = ReadString(remainder, out remainder);

                result += (result.Length > 0 ? "::" : "") + current;
            }

            return result;
        }

        /* e.g. "13move__tm__2_wv" => ("move<wchar_t>", "v") */
        static string ReadString(string name, out string remainder)
        {
            if (name.Length == 0)
                throw new InvalidDataException("Unexpected end of string. Expected a digit.");

            int len = ReadInt(name, out name);
            if (len == 0 || name.Length < len)
                throw new InvalidDataException("Bad string length \"" + len.ToString(CultureInfo.InvariantCulture) + "\".");

            remainder = name.Substring(len);
            return DemangleTemplate(name.Substring(0, len));
        }

        /* e.g. "move__tm__2_w" => "move<wchar_t>" */
        static string DemangleTemplate(string name)
        {
            int mstart;

            mstart = name.IndexOf("__", 1, StringComparison.Ordinal);

            /* check for something like "h___tm_2_i" => "h_<int>" */
            if (mstart != -1 && name.Substring(mstart).StartsWith("___", StringComparison.Ordinal))
                mstart++;

            /* not a special symbol name! */
            if (mstart == -1)
                return name;

            /* something more interesting! */
            string remainder = name.Substring(mstart + 2);
            name = name.Substring(0, mstart);

            while (true)
            {
                if (!StartsWithAny(remainder, templatePrefixes, StringComparison.Ordinal))
                    throw new InvalidDataException("Unexpected template argument prefix.");

                /* format of remainder should be <type>__<len>_<arg> */
                int lstart = remainder.IndexOf("__", StringComparison.Ordinal);

                if (lstart == -1)
                    throw new InvalidDataException("Bad template argument: \"" + remainder + "\".");

                remainder = remainder.Substring(lstart + 2);

                int len = ReadInt(remainder, out remainder);

                if (len == 0 || len > remainder.Length)
                    throw new InvalidDataException("Bad template argument length: \"" + len.ToString(CultureInfo.InvariantCulture) + "\".");
                if (!remainder.StartsWith("_"))
                    throw new InvalidDataException("Unexpected character after template argument length \"" + remainder[0] + "\". Expected \"_\".");

                string tmp;
                string declArgs = ReadTemplateArguments(remainder.Substring(1), out tmp);

                /* avoid emitting the ">>" token */
                if (declArgs.EndsWith(">", StringComparison.Ordinal))
                    declArgs += " ";

                name += "<" + declArgs + ">";
                remainder = remainder.Substring(len);

                if (tmp != remainder)
                    throw new InvalidDataException("Bad template argument length: \"" + len.ToString(CultureInfo.InvariantCulture) + "\".");

                /* check if we've hit the end */
                if (remainder.Length == 0)
                    return name;

                /* should be immediately followed with __ */
                if (!remainder.StartsWith("__", StringComparison.Ordinal))
                    throw new InvalidDataException("Unexpected character(s) after template: \"" + remainder[0] + "\". Expected \"__\".");
                remainder = remainder.Substring(2);
            }
        }

        /* e.g. "4move" => (4, "move") */
        static int ReadInt(string name, out string remainder)
        {
            if (name.Length == 0)
                throw new InvalidDataException("Unexpected end of string. Expected a digit.");
            if (!char.IsDigit(name[0]))
                throw new InvalidDataException("Unexpected character \"" + name[0] + "\". Expected a digit.");

            int i = 1;
            while (i < name.Length && char.IsDigit(name[i])) i++;

            remainder = name.Substring(i);
            return int.Parse(name.Substring(0, i), CultureInfo.InvariantCulture);
        }

        /* helper method for starts with on an array */
        static bool StartsWithAny(string str, string[] names, StringComparison c)
        {
            foreach (var s in names)
                if (str.StartsWith(s, c))
                    return true;
            return false;
        }
    }
}
