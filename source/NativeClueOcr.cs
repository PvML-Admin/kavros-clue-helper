using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using Windows.Foundation;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace KavrosClueHelper
{
    // Called only by the analysis worker. All WinRT operations have a deadline
    // and honour cancellation; no network service or game input is involved.
    public sealed class NativeClueOcr
    {
        private OcrEngine engine;
        private bool initialized;
        public string Unavailable;
        private T Wait<T>(IAsyncOperation<T> operation)
        {
            Stopwatch timer=Stopwatch.StartNew();
            try
            {
                while(operation.Status==AsyncStatus.Started)
                {
                    SearchCancellation.Check();
                    if(timer.ElapsedMilliseconds>2500)throw new TimeoutException("Windows text recognition timed out.");
                    Thread.Sleep(2);
                }
                SearchCancellation.Check();return operation.GetResults();
            }
            catch {operation.Cancel();throw;}
            finally{operation.Close();}
        }
        public string Read(Bitmap image)
        {
            if(Thread.CurrentThread.GetApartmentState()==ApartmentState.STA)
            {
                var token=SearchCancellation.Token;
                return System.Threading.Tasks.Task.Run(()=>{SearchCancellation.Token=token;try{return ReadCore(image);}finally{SearchCancellation.Token=CancellationToken.None;}}).GetAwaiter().GetResult();
            }
            return ReadCore(image);
        }
        private string ReadCore(Bitmap image)
        {
            try{return Recognize(image);}
            catch(OperationCanceledException){throw;}
            catch(Exception e){Unavailable=e is TimeoutException?e.Message:"Windows English text recognition is unavailable. Enable the English OCR language in Windows Settings.";return null;}
        }
        private string Recognize(Bitmap image)
        {
            if(!initialized)
            {
                initialized=true;
                engine=OcrEngine.TryCreateFromLanguage(new Language("en-US"));
                if(engine==null){Unavailable="Enable English OCR in Windows Settings to read text clues.";return null;}
            }
            if(engine==null)return null;
            using(var bytes=new MemoryStream())
            using(var stream=new InMemoryRandomAccessStream())
            {
                image.Save(bytes,ImageFormat.Png);
                using(var writer=new DataWriter(stream.GetOutputStreamAt(0)))
                {writer.WriteBytes(bytes.ToArray());Wait<uint>(writer.StoreAsync());Wait<bool>(writer.FlushAsync());writer.DetachStream();}
                stream.Seek(0);BitmapDecoder decoder=Wait<BitmapDecoder>(BitmapDecoder.CreateAsync(stream));
                using(SoftwareBitmap bitmap=Wait<SoftwareBitmap>(decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied)))
                {return Wait<OcrResult>(engine.RecognizeAsync(bitmap)).Text;}
            }
        }
    }
}
