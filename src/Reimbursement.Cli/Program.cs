using System.Text;
using Reimbursement.Cli;

Console.InputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

var runner = new CliRunner();
return runner.Run(args, Console.In, Console.Out, Console.Error);
