﻿using System;
using System.Collections.Generic;
using System.Linq;
using static LanguageExt.Prelude;

namespace LanguageExt
{
    /// <summary>
    /// Writer monad
    /// </summary>
    /// <typeparam name="Out">Writer output</typeparam>
    /// <typeparam name="T">Wrapped type</typeparam>
    public delegate WriterResult<Out, T> Writer<Out, T>();

    public struct WriterResult<Out, T>
    {
        public readonly T Value;
        public readonly IEnumerable<Out> Output;
        public readonly bool IsBottom;

        internal WriterResult(T value, IEnumerable<Out> output, bool isBottom = false)
        {
            if (output == null) throw new ArgumentNullException("output");
            Value = value;
            Output = output;
            IsBottom = isBottom;
        }

        public static implicit operator WriterResult<Out, T>(T value) =>
           new WriterResult<Out, T>(value, new Out[0]);

        public static implicit operator T(WriterResult<Out, T> value) =>
           value.Value;
    }

    /// <summary>
    /// Writer extension methods
    /// </summary>
    public static class WriterExt
    {
        public static IEnumerable<T> AsEnumerable<Out, T>(this Writer<Out, T> self)
        {
            var res = self();
            if (!res.IsBottom)
            {
                yield return self().Value;
            }
        }

        public static Writer<Out,Unit> Iter<Out, T>(this Writer<Out, T> self, Action<T> action)
        {
            return () =>
            {
                var res = self();
                if (!res.IsBottom)
                {
                    action(res.Value);
                }
                return unit;
            };
        }

        public static Writer<Out,int> Count<Out, T>(this Writer<Out, T> self) => () =>
            bmap(self(), x => 1);

        public static Writer<Out, bool> ForAll<Out, T>(this Writer<Out, T> self, Func<T, bool> pred) => () =>
            bmap(self(), x => pred(x));

        public static Writer<Out,bool> Exists<Out, T>(this Writer<Out, T> self, Func<T, bool> pred) => () =>
            bmap(self(), x => pred(x));

        public static Writer<Out, S> Fold<Out, S, T>(this Writer<Out, T> self, S state, Func<S, T, S> folder) => () =>
            bmap(self(), x => folder(state, x));

        public static Writer<Out, R> Map<Out, T, R>(this Writer<Out, T> self, Func<T, R> mapper) =>
            self.Select(mapper);

        public static Writer<Out, R> Bind<Out, T, R>(this Writer<Out, T> self, Func<T, Writer<Out, R>> binder) =>
            from x in self
            from y in binder(x)
            select y;

        /// <summary>
        /// Select
        /// </summary>
        public static Writer<W, U> Select<W, T, U>(this Writer<W, T> self, Func<T, U> select)
        {
            if (select == null) throw new ArgumentNullException("select");
            return () =>
            {
                var resT = self();
                if (resT.IsBottom) return new WriterResult<W, U>(default(U), resT.Output, true);
                var resU = select(resT.Value);
                return new WriterResult<W, U>(resU, resT.Output);
            };
        }

        /// <summary>
        /// Select Many
        /// </summary>
        public static Writer<W, V> SelectMany<W, T, U, V>(
            this Writer<W, T> self,
            Func<T, Writer<W, U>> bind,
            Func<T, U, V> project
        )
        {
            if (bind == null) throw new ArgumentNullException("bind");
            if (project == null) throw new ArgumentNullException("project");

            return () =>
            {
                var resT = self();
                if (resT.IsBottom) return new WriterResult<W, V>(default(V), resT.Output, true);
                var resU = bind(resT.Value).Invoke();
                if (resT.IsBottom) return new WriterResult<W, V>(default(V), resU.Output, true);
                var resV = project(resT.Value, resU.Value);
                return new WriterResult<W, V>(resV, resT.Output.Concat(resU.Output));
            };
        }

        public static Writer<W, T> Filter<W, T>(this Writer<W, T> self, Func<T, bool> pred) =>
            self.Where(pred);

        public static Writer<W, T> Where<W, T>(this Writer<W, T> self, Func<T, bool> pred)
        {
            return () =>
            {
                var res = self();
                return new WriterResult<W, T>(res.Value, res.Output, !pred(res.Value));
            };
        }

        public static Writer<W, int> Sum<W>(this Writer<W, int> self) =>
            () => bmap(self(), x => x);

        private static WriterResult<W, R> bmap<W, T, R>(WriterResult<W, T> r, Func<T, R> f) =>
            r.IsBottom
                ? new WriterResult<W, R>(default(R), r.Output, true)
                : new WriterResult<W, R>(f(r.Value), r.Output, false);

        private static WriterResult<W, Unit> bmap<W, T>(WriterResult<W, T> r, Action<T> f)
        {
            if (r.IsBottom)
            {
                return new WriterResult<W, Unit>(unit, r.Output, true);
            }
            else
            {
                f(r.Value);
                return new WriterResult<W, Unit>(unit, r.Output, false);
            }
        }

        /// <summary>
        /// Select Many - IEnumerable
        /// </summary>
        public static Writer<W, IEnumerable<V>> SelectMany<W, T, U, V>(
            this Writer<W, T> self,
            Func<T, IEnumerable<U>> bind,
            Func<T, U, V> project
        )
        {
            if (bind == null) throw new ArgumentNullException("bind");
            if (project == null) throw new ArgumentNullException("project");

            return () =>
            {
                var resT = self();
                if (resT.IsBottom) return new WriterResult<W, IEnumerable<V>>(List<V>(), resT.Output, true);
                var resU = bind(resT.Value);
                return new WriterResult<W, IEnumerable<V>>(resU.Select(x => project(resT.Value, x)), resT.Output);
            };
        }

        /// <summary>
        /// Select Many - Map
        /// </summary>
        public static Writer<W, Map<K,V>> SelectMany<W, K, T, U, V>(
            this Writer<W, T> self,
            Func<T, Map<K,U>> bind,
            Func<T, U, V> project
        )
        {
            if (bind == null) throw new ArgumentNullException("bind");
            if (project == null) throw new ArgumentNullException("project");

            return () =>
            {
                var resT = self();
                if (resT.IsBottom) return new WriterResult<W, Map<K, V>>(Map<K, V>(), resT.Output, true);
                var resU = bind(resT.Value);
                return new WriterResult<W, Map<K,V>>(resU.Select(x => project(resT.Value, x)), resT.Output);
            };
        }
        /// <summary>
        /// Select Many - Lst
        /// </summary>
        public static Writer<W, Lst<V>> SelectMany<W, T, U, V>(
            this Writer<W, T> self,
            Func<T, Lst<U>> bind,
            Func<T, U, V> project
        )
        {
            if (bind == null) throw new ArgumentNullException("bind");
            if (project == null) throw new ArgumentNullException("project");

            return () =>
            {
                var resT = self();
                if (resT.IsBottom) return new WriterResult<W, Lst<V>>(List<V>(), resT.Output, true);
                var resU = bind(resT.Value);
                var resV = resU.Select(x => project(resT.Value, x));
                return new WriterResult<W, Lst<V>>(List.createRange(resV), resT.Output);
            };
        }

        /// <summary>
        /// Select Many - Option
        /// </summary>
        public static Writer<W, Option<V>> SelectMany<W, T, U, V>(
            this Writer<W, T> self,
            Func<T, Option<U>> bind,
            Func<T, U, V> project
        )
        {
            if (bind == null) throw new ArgumentNullException("bind");
            if (project == null) throw new ArgumentNullException("project");

            return () =>
            {
                var resT = self();
                if (resT.IsBottom) return new WriterResult<W, Option<V>>(None, resT.Output, true);
                var resU = bind(resT.Value);
                if (resU.IsNone) return new WriterResult<W, Option<V>>(default(Option<V>), resT.Output, true);
                return new WriterResult<W, Option<V>>(project(resT.Value, resU.Value), resT.Output);
            };
        }

        /// <summary>
        /// Select Many - OptionUnsafe
        /// </summary>
        public static Writer<W, OptionUnsafe<V>> SelectMany<W, T, U, V>(
            this Writer<W, T> self,
            Func<T, OptionUnsafe<U>> bind,
            Func<T, U, V> project
        )
        {
            if (bind == null) throw new ArgumentNullException("bind");
            if (project == null) throw new ArgumentNullException("project");

            return () =>
            {
                var resT = self();
                if (resT.IsBottom) return new WriterResult<W, OptionUnsafe<V>>(None, resT.Output, true);
                var resU = bind(resT.Value);
                if (resU.IsNone) return new WriterResult<W, OptionUnsafe<V>>(default(OptionUnsafe<V>), resT.Output, true);
                return new WriterResult<W, OptionUnsafe<V>>(project(resT.Value, resU.Value), resT.Output);
            };
        }

        /// <summary>
        /// Select Many - Try
        /// </summary>
        public static Writer<W, Try<V>> SelectMany<W, T, U, V>(
            this Writer<W, T> self,
            Func<T, Try<U>> bind,
            Func<T, U, V> project
        )
        {
            if (bind == null) throw new ArgumentNullException("bind");
            if (project == null) throw new ArgumentNullException("project");

            return () =>
            {
                var resT = self();
                if (resT.IsBottom)
                {
                    return new WriterResult<W, Try<V>>(default(Try<V>), resT.Output, true);
                }
                return new WriterResult<W, Try<V>>(() =>
                {
                    try
                    {
                        var resU = bind(resT.Value)();
                        if (resU.IsFaulted)
                        {
                            return new TryResult<V>(resU.Exception);
                        }
                        return new TryResult<V>(project(resT.Value, resU.Value));
                    }
                    catch (Exception e)
                    {
                        return new TryResult<V>(e);
                    }
                },
                resT.Output);
            };
        }

        /// <summary>
        /// Select Many - TryOption
        /// </summary>
        public static Writer<W, TryOption<V>> SelectMany<W, T, U, V>(
            this Writer<W, T> self,
            Func<T, TryOption<U>> bind,
            Func<T, U, V> project
        )
        {
            if (bind == null) throw new ArgumentNullException("bind");
            if (project == null) throw new ArgumentNullException("project");

            return () =>
            {
                var resT = self();
                if (resT.IsBottom)
                {
                    return new WriterResult<W, TryOption<V>>(default(TryOption<V>), resT.Output, true);
                }
                return new WriterResult<W, TryOption<V>>(() =>
                {
                    try
                    {
                        var resU = bind(resT.Value)();
                        if (resU.IsFaulted)
                        {
                            return new TryOptionResult<V>(resU.Exception);
                        }
                        if (resU.Value.IsNone)
                        {
                            return new TryOptionResult<V>(None);
                        }
                        return new TryOptionResult<V>(project(resT.Value, resU.Value.Value));
                    }
                    catch (Exception e)
                    {
                        return new TryOptionResult<V>(e);
                    }
                },
                resT.Output);
            };
        }

        /// <summary>
        /// Select Many - Either
        /// </summary>
        public static Writer<W, Either<L, V>> SelectMany<W, L, T, U, V>(
            this Writer<W, T> self,
            Func<T, Either<L, U>> bind,
            Func<T, U, V> project
        )
        {
            if (bind == null) throw new ArgumentNullException("bind");
            if (project == null) throw new ArgumentNullException("project");

            return () =>
            {
                var resT = self();
                if (resT.IsBottom) return new WriterResult<W, Either<L, V>>(default(Either<L, V>), resT.Output, true);
                var resU = bind(resT.Value);
                if (resU.IsLeft) return new WriterResult<W, Either<L, V>>(default(Either<L, V>), resT.Output, true);
                return new WriterResult<W, Either<L, V>>(project(resT.Value, resU.RightValue), resT.Output);
            };
        }

        /// <summary>
        /// Select Many - EitherUnsafe
        /// </summary>
        public static Writer<W, EitherUnsafe<L, V>> SelectMany<W, L, T, U, V>(
            this Writer<W, T> self,
            Func<T, EitherUnsafe<L, U>> bind,
            Func<T, U, V> project
        )
        {
            if (bind == null) throw new ArgumentNullException("bind");
            if (project == null) throw new ArgumentNullException("project");

            return () =>
            {
                var resT = self();
                if (resT.IsBottom) return new WriterResult<W, EitherUnsafe<L, V>>(default(EitherUnsafe<L, V>), resT.Output, true);
                var resU = bind(resT.Value);
                if (resU.IsLeft) return new WriterResult<W, EitherUnsafe<L, V>>(default(EitherUnsafe<L, V>), resT.Output, true);
                return new WriterResult<W, EitherUnsafe<L, V>>(project(resT.Value, resU.RightValue), resT.Output);
            };
        }

        /// <summary>
        /// Select Many - State
        /// </summary>
        public static Writer<W, State<S,V>> SelectMany<W, S, T, U, V>(
            this Writer<W, T> self,
            Func<T, State<S, U>> bind,
            Func<T, U, V> project
        )
        {
            if (bind == null) throw new ArgumentNullException("bind");
            if (project == null) throw new ArgumentNullException("project");

            return () =>
            {
                var resT = self();
                if (resT.IsBottom)
                {
                    return new WriterResult<W, State<S, V>>(default(State<S, V>), resT.Output, true);
                }
                return new WriterResult<W, State<S, V>>((S state) =>
                {
                    var resU = bind(resT.Value)(state);
                    if (resU.IsBottom)
                    {
                        return new StateResult<S, V>(state, default(V), true);
                    }
                    return new StateResult<S, V>(resU.State, project(resT.Value, resU.Value), false);
                },
                resT.Output);
            };
        }

        /// <summary>
        /// Select Many - Reader
        /// </summary>
        public static Writer<W, Reader<Env, V>> SelectMany<W, Env, T, U, V>(
            this Writer<W, T> self,
            Func<T, Reader<Env, U>> bind,
            Func<T, U, V> project
        )
        {
            if (bind == null) throw new ArgumentNullException("bind");
            if (project == null) throw new ArgumentNullException("project");

            return () =>
            {
                var resT = self();
                if (resT.IsBottom)
                {
                    return new WriterResult<W, Reader<Env, V>>(default(Reader<Env, V>), resT.Output, true);
                }
                return new WriterResult<W, Reader<Env, V>>((Env env) =>
                {
                    var resU = bind(resT.Value)(env);
                    if (resU.IsBottom)
                    {
                        return new ReaderResult<V>(default(V), true);
                    }
                    return new ReaderResult<V>(project(resT.Value, resU.Value));
                }, resT.Output);
            };
        }
    }
}
