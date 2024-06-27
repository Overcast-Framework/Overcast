using LLVMSharp.Interop;
using Overcast;
using Overcast.CodeAnalysis.LLVMC;
using Overcast.CodeAnalysis.Parsing;
using Overcast.CodeAnalysis.Semantic;
using Overcast.CodeAnalysis.Tokenization;
using Serilog;
using Serilog.Core;

namespace occ
{
    internal class Program
    {
        internal static Logger log = new LoggerConfiguration().WriteTo.Console().CreateLogger();
        static unsafe void Main(string[] args)
        {
            DateTimeOffset start = DateTimeOffset.Now;
            try
            {
                Lexer lexer = new Lexer();

                string code = File.ReadAllText("test01.oc");

                DateTimeOffset startt = DateTimeOffset.Now;
                var tks = lexer.MatchTokens(code);
                Console.WriteLine("Lexer finished in " + (DateTimeOffset.Now - startt).TotalMilliseconds + " ms.");

                DateTimeOffset startp = DateTimeOffset.Now;
                var prsrData = new Parser().ParseTokens(tks);
                Console.WriteLine("Parser finished in " + (DateTimeOffset.Now - startp).TotalMilliseconds + " ms.");

                DateTimeOffset startb = DateTimeOffset.Now;
                new Binder().RunAnalysis(prsrData);
                Console.WriteLine("Binder finished in " + (DateTimeOffset.Now - startb).TotalMilliseconds + " ms.");

                DateTimeOffset startl = DateTimeOffset.Now;
                var llvmEmitter = new LLVMEmitter();
                llvmEmitter.Initialize_NatAsm("test01");

                var llvmModule = llvmEmitter.EmitModule(prsrData);
                Console.WriteLine("LLVM IR Generation finished in " + (DateTimeOffset.Now - startl).TotalMilliseconds + " ms.");
                sbyte* errorCode = null;

                LLVM.PrintModuleToFile(llvmModule, OCHelper.StrToSByte("test01.ll"), &errorCode);
                if (errorCode is not null)
                {
                    Console.WriteLine("Error while printing module to file: " + OCHelper.SByteToStr(errorCode));
                }

                Console.WriteLine("Total elapsed time " + (DateTimeOffset.Now - start).TotalMilliseconds + " ms.");

                llvmEmitter.Dispose();
            }
            catch (ParserException e)
            {
                Console.WriteLine("An error occured during compilation.");
                log.Error(e.Message);
                log.Information("Stack Trace:\n" + e.StackTrace);
            }
            catch (TokenMismatchException e)
            {
                Console.WriteLine("An error occured during compilation.");
                log.Error(e.Message);
                log.Information("Stack Trace:\n"+e.StackTrace);
            }

            catch (BinderException binderE)
            {
                Console.WriteLine("An error occured during compilation.");
                log.Error(binderE.Message);
                return;
            }
        }
    }
}
