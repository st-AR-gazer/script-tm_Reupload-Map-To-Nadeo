using GBX.NET;
using GBX.NET.LZO;
using ReuploadMapToNadeo;

Gbx.LZO = new Lzo();

return await CliApplication.RunAsync(args);
