﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NewLife.NoDb.IO
{
    /// <summary>内存视图</summary>
    public class MemoryView : DisposeBase
    {
        #region 属性
        /// <summary>内存文件</summary>
        public MemoryFile File { get; }

        /// <summary>偏移</summary>
        public Int64 Offset { get; private set; }

        /// <summary>当前大小</summary>
        public Int64 Size { get; private set; }

        /// <summary>最大容量</summary>
        public Int64 Capacity { get; private set; }

        private MemoryMappedViewAccessor _view;
        #endregion

        #region 构造
        /// <summary>实例化一个内存视图</summary>
        /// <param name="file"></param>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        public MemoryView(MemoryFile file, Int64 offset, Int64 size)
        {
            File = file ?? throw new ArgumentNullException(nameof(file));
            Offset = offset;
            Capacity = size;
        }

        /// <summary>销毁</summary>
        /// <param name="disposing"></param>
        protected override void OnDispose(Boolean disposing)
        {
            base.OnDispose(disposing);

            _view.TryDispose();
            _view = null;
        }
        #endregion

        #region 读写
        /// <summary>获取视图，自动扩大</summary>
        /// <param name="offset">内存偏移</param>
        /// <param name="size">内存大小</param>
        /// <returns></returns>
        public MemoryMappedViewAccessor GetView(Int64 offset, Int64 size)
        {
            // 如果在已有范围内，则直接返回
            var maxsize = offset + size;
            if (_view != null && maxsize <= Size) return _view;

            // 扩大视图
            size = maxsize + Offset;
            if (size < 1024)
                size = 1024;
            else
            {
                var n = size % 1024;
                if (n > 0) size += 1024 - n;
            }

            Size = size - Offset;

            // 容量检查
            if (Capacity > 0 && Size > Capacity) throw new ArgumentOutOfRangeException(nameof(Size));

            // 销毁旧的
            _view.TryDispose();

            // 映射文件扩容
            File.CheckCapacity(Offset + Size);

            return _view = File.Map.CreateViewAccessor(Offset, Size);
        }

        /// <summary>读取长整数</summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public Int32 ReadInt32(Int64 position)
        {
            var view = GetView(position, 4);
            return view.ReadInt32(position);
        }

        /// <summary>写入长整数</summary>
        /// <param name="position"></param>
        /// <param name="value"></param>
        public void Write(Int64 position, Int32 value)
        {
            var view = GetView(position, 4);
            view.Write(position, value);
        }

        /// <summary>读取长整数</summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public Int64 ReadInt64(Int64 position)
        {
            var view = GetView(position, 8);
            return view.ReadInt64(position);
        }

        /// <summary>写入长整数</summary>
        /// <param name="position"></param>
        /// <param name="value"></param>
        public void Write(Int64 position, Int64 value)
        {
            var view = GetView(position, 8);
            view.Write(position, value);
        }

        /// <summary>读取结构体</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="position"></param>
        /// <param name="structure"></param>
        public void Read<T>(Int64 position, out T structure) where T : struct
        {
            var view = GetView(position, SizeOf<T>());
            view.Read(position, out structure);
        }

        /// <summary>写入结构体</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="position"></param>
        /// <param name="structure"></param>
        public void Write<T>(Int64 position, ref T structure) where T : struct
        {
            var view = GetView(position, SizeOf<T>());
            view.Write(position, ref structure);
        }

        /// <summary>读取数组</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="position">位置</param>
        /// <param name="array">数组</param>
        /// <param name="offset">偏移</param>
        /// <param name="count">个数</param>
        public void ReadArray<T>(Int64 position, T[] array, Int32 offset, Int32 count) where T : struct
        {
            var view = GetView(position, SizeOf<T>() * count);
            view.ReadArray(position, array, offset, count);
        }

        /// <summary>写入数组</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="position">位置</param>
        /// <param name="array">数组</param>
        /// <param name="offset">偏移</param>
        /// <param name="count">个数</param>
        public void WriteArray<T>(Int64 position, T[] array, Int32 offset, Int32 count) where T : struct
        {
            var view = GetView(position, SizeOf<T>() * count);
            view.WriteArray(position, array, offset, count);
        }

        /// <summary>读取字节数组</summary>
        /// <param name="position"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public unsafe Byte[] ReadBytes(Int64 position, Int32 count)
        {
            var view = GetView(position, count);

            var ptr = (Byte*)0;
            view.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
            try
            {
                var p = new IntPtr(ptr);
                p = new IntPtr(p.ToInt64() + position);
                var arr = new Byte[count];
                Marshal.Copy(p, arr, 0, count);
                return arr;
            }
            finally
            {
                view.SafeMemoryMappedViewHandle.ReleasePointer();
            }

            //var arr = new Byte[num];
            //view.ReadArray(offset, arr, 0, num);

            //return arr;
        }

        /// <summary>写入字节数组</summary>
        /// <param name="position"></param>
        /// <param name="data"></param>
        public unsafe void WriteBytes(Int64 position, Byte[] data)
        {
            var view = GetView(position, data.Length);

            var ptr = (Byte*)0;
            view.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
            try
            {
                var p = new IntPtr(ptr);
                p = new IntPtr(p.ToInt64() + position);
                Marshal.Copy(data, 0, p, data.Length);
            }
            finally
            {
                view.SafeMemoryMappedViewHandle.ReleasePointer();
            }
        }
        #endregion

        #region 辅助
        private static ConcurrentDictionary<Type, Int32> _sizeCache = new ConcurrentDictionary<Type, Int32>();
        private static Int32 SizeOf<T>()
        {
            return _sizeCache.GetOrAdd(typeof(T), t => Marshal.SizeOf(t));
        }
        #endregion
    }
}