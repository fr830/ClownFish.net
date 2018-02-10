﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClownFish.Base.WebClient;

namespace ClownFish.Base
{
    /// <summary>
    /// 用于执行重试任务的工具类
    /// </summary>
    public sealed class Retry
    {
        private int _retryCount;

        private static readonly int DefaultMilliseconds = 1000;

        private List<Func<Exception, bool>> _filterList = null;
        private List<Action<Exception, int>> _callbakList = null;


        //private void 示例代码()
        //{
        //    string text = Retry.Create().Run(()=> {
        //        return System.IO.File.ReadAllText(@"c:\aa.txt", Encoding.UTF8);
        //    });

        //    string text2 = 
        //        Retry.Create(3)
        //            .Filter<RemoteWebException>()
        //            .Run(() => {
        //                return new HttpOption {
        //                    Method = "POST",
        //                    Url = "http://www.abc.com/test.aspx",
        //                    Data = new { a = 1, b = 2, c = "Fish Li" },
        //                    Headers = new Dictionary<string, string>() {
        //                        { "X-Requested-With", "XMLHttpRequest" },
        //                        { "User-Agent", "Mozilla/5.0 (Windows NT 6.3; WOW64)"} }
        //                }.GetResult();
        //        });


        //    object result =
        //        Retry.Create(3)
        //            .Filter<ArgumentException>()
        //            .Filter<System.Data.SqlClient.SqlException>(ex => ex.Number == 1205)
        //            .OnException((ex, n) => { /* log exception */ return; })
        //            .Run(() => {
        //                // 执行逻辑任务，返回结果
        //                return "result";
        //            });


        //    // 更多示例可参考： RetryTest.cs
        //}


        /// <summary>
        /// 创建RetryOption实例
        /// </summary>
        /// <param name="count"></param>
        /// <param name="milliseconds"></param>
        /// <returns></returns>
        public static Retry Create(int count = 5, int milliseconds = 0)
        {
            return new Retry { Count = count, Milliseconds = milliseconds };
        }



        /// <summary>
        /// 重试次数
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// 二次重试之间的间隔毫秒。
        /// 如果不指定，默认 1 秒。
        /// </summary>
        public int Milliseconds { get; set; }


        /// <summary>
        /// 设置仅重试哪些类型的异常。
        /// 允许多次调用（以OR方式处理）。
        /// </summary>
        /// <typeparam name="TException"></typeparam>
        /// <returns></returns>
        public Retry Filter<TException>() where TException : Exception
        {
            if( _filterList == null )
                _filterList = new List<Func<Exception, bool>>();


            _filterList.Add(
                (Exception ex) => {
                    TException tex = ex as TException;
                    if( tex == null )
                        return false;

                    return true;
                });

            return this;
        }

        /// <summary>
        /// 设置仅重试哪些类型的异常，并允许根据特殊的异常再进一步判断。
        /// 允许多次调用（以OR方式处理）。
        /// </summary>
        /// <typeparam name="TException"></typeparam>
        /// <param name="func"></param>
        /// <returns></returns>
        public Retry Filter<TException>(Func<TException, bool> func) where TException : Exception
        {
            if( func == null )
                throw new ArgumentNullException(nameof(func));

            if( _filterList == null )
                _filterList = new List<Func<Exception, bool>>();


            _filterList.Add(
                (Exception ex) => {
                    TException tex = ex as TException;
                    if( tex == null )
                        return false;

                    return func(tex);
                });

            return this;
        }

        /// <summary>
        /// 当异常发生并需要重试时执行的回调委托
        /// </summary>
        /// <param name="callback"></param>
        /// <returns></returns>
        public Retry OnException(Action<Exception, int> callback)
        {
            if( callback == null )
                throw new ArgumentNullException(nameof(callback));

            if( _callbakList == null )
                _callbakList = new List<Action<Exception, int>>();

            _callbakList.Add(callback);
            return this;
        }



       
 

        /// <summary>
        /// 执行重试任务
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        /// <returns></returns>
        public T Run<T>(Func<T> func)
        {
            if( func == null )
                throw new ArgumentNullException(nameof(func));

            // 重试次数设置不正确，直接调用（不做异常处理）
            if( this.Count <= 0 )
                return func();


            while( true ) {
                try {
                    return func();
                }
                catch(Exception ex) {
                    if( CheckCount(ex) == false ) {
                        // 如果超过重试次数，就抛出本次捕获的异常，结束整个重试机制
                        throw;
                    }
                }
            }
        }

        
        /// <summary>
        /// 执行重试任务
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        /// <returns></returns>
        public async Task<T> RunAsync<T>(Func<Task<T>> func)
        {
            if( func == null )
                throw new ArgumentNullException(nameof(func));

            // 重试次数设置不正确，直接调用（不做异常处理）
            if( this.Count <= 0 )
                return await func();


            while( true ) {
                try {
                    return await func();
                }
                catch( Exception ex ) {
                    if( CheckCount(ex) == false ) {
                        // 如果超过重试次数，就抛出本次捕获的异常，结束整个重试机制
                        throw;
                    }
                }
            }
        }


        private bool CheckFilter(Exception ex)
        {
            // 如果没有定义过滤条件，就认为需要重试
            if( _filterList == null || _filterList.Count == 0 )
                return true;


            foreach( var func in _filterList ) {

                // 只要满足一个过滤条件就认为是有效的异常，需要执行重试
                if( func(ex) ) {
                    return true;
                }
            }

            // 所有过滤条件都不满足
            return false;
        }

        private bool CheckCount(Exception ex)
        {
            // 如果不满足过滤条件，就直接跳出，最终也不会被重试
            if( CheckFilter(ex) == false ) {
                return false;
            }

            

            // 如果在重试次数之内，就启动重试机制
            if( _retryCount < this.Count ) {
                _retryCount++;

                // 为了保证重试有效，先暂停，等待外部环境变化
                if( this.Milliseconds <= 0 )
                    // 如果不指定间隔时间，就取默认值
                    System.Threading.Thread.Sleep(DefaultMilliseconds);
                else
                    System.Threading.Thread.Sleep(this.Milliseconds);

                
                // 执行回调
                if( _callbakList != null ) {
                    foreach( var cb in _callbakList )
                        cb(ex, _retryCount);
                }

                return true;
            }
            else {
                return false;
            }
        }


    }
}