using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using CefSharp;
using CefSharp.Wpf;

namespace CefSharp.Wpf.Rendering
{
    public class CompositionTargetRenderHandler : IDisposable, IRenderHandler
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, EntryPoint = "CopyMemory")]
        private static extern void CopyMemory(IntPtr destination, IntPtr source, int length);

        private readonly RenderingLayer _background;
        private readonly RenderingLayer _popup;

        public CompositionTargetRenderHandler(double dpiX = 96, double dpiY = 96)
        {
            _background = new RenderingLayer() { _dpiX = dpiX, _dpiY = dpiY };
            _popup = new RenderingLayer() { _dpiX = dpiX, _dpiY = dpiY };
        }

        private class RenderingLayer
        {
            public double _dpiX;
            public double _dpiY;
            private object _lock = new object();
            private int _width = 0;
            private int _height = 0;
            private IntPtr _buffer = IntPtr.Zero;
            private int _bufferSize = 0;
            private Image _image = null;

            public RenderingLayer()
            {
                System.Windows.Media.CompositionTarget.Rendering += this.OnRendering;
            }
            public void Dispose()
            {
                if (_buffer != IntPtr.Zero)
                {
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(_buffer);
                    _buffer = IntPtr.Zero;
                }

                System.Windows.Media.CompositionTarget.Rendering -= this.OnRendering;
            }

            public void OnRendering(object sender, EventArgs args)
            {
                lock (_lock)
                {
                    if (_image != null)
                    {
                        var writeableBitmap = _image.Source as WriteableBitmap;
                        if (writeableBitmap == null || writeableBitmap.PixelWidth != _width || writeableBitmap.PixelHeight != _height)
                        {
                            _image.Source = writeableBitmap = new WriteableBitmap(_width, _height, _dpiX, _dpiY, System.Windows.Media.PixelFormats.Pbgra32, null);
                        }

                        if (writeableBitmap != null)
                        {
                            writeableBitmap.Lock();

                            CopyMemory(writeableBitmap.BackBuffer, _buffer, writeableBitmap.BackBufferStride * writeableBitmap.PixelHeight);

                            writeableBitmap.AddDirtyRect(new System.Windows.Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
                            writeableBitmap.Unlock();
                        }

                        _image = null;  //Validate
                    }
                }
            }

            public void OnPaint(CefSharp.Structs.Rect dirtyRect, IntPtr buffer, int width, int height, Image image)
            {
                lock (_lock)
                {
                    int numberOfBytes = (width * height) * (System.Windows.Media.PixelFormats.Pbgra32.BitsPerPixel / 8);

                    if (_bufferSize < numberOfBytes)
                    {
                        System.Runtime.InteropServices.Marshal.FreeHGlobal(_buffer);
                        _buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(numberOfBytes);
                        _bufferSize = numberOfBytes;
                    }

                    _width = width;
                    _height = height;

                    CopyMemory(_buffer, buffer, numberOfBytes);

                    _image = image; //Invalidate
                }
            }
        };

        public void Dispose()
        {
            _background.Dispose();
            _popup.Dispose();
        }

        public void OnAcceleratedPaint(bool isPopup, CefSharp.Structs.Rect dirtyRect, IntPtr sharedHandle)
        {
            //throw new NotImplementedException();
        }

        public void OnPaint(bool isPopup, CefSharp.Structs.Rect dirtyRect, IntPtr buffer, int width, int height, Image image)
        {
            var layer = isPopup ? this._popup : this._background;
            layer.OnPaint(dirtyRect, buffer, width, height, image);
        }
    }
}
