﻿// zlib/libpng License
//
// Copyright (c) 2018 Sinoa
//
// This software is provided 'as-is', without any express or implied warranty.
// In no event will the authors be held liable for any damages arising from the use of this software.
// Permission is granted to anyone to use this software for any purpose,
// including commercial applications, and to alter it and redistribute it freely,
// subject to the following restrictions:
//
// 1. The origin of this software must not be misrepresented; you must not claim that you wrote the original software.
//    If you use this software in a product, an acknowledgment in the product documentation would be appreciated but is not required.
// 2. Altered source versions must be plainly marked as such, and must not be misrepresented as being the original software.
// 3. This notice may not be removed or altered from any source distribution.

using System;
using System.Collections.Generic;
using System.Threading;

namespace IceMilkTea.Core
{
    /// <summary>
    /// コンテキストを持つことのできるステートマシンクラスです
    /// </summary>
    /// <typeparam name="TContext">このステートマシンが持つコンテキストの型</typeparam>
    public class ImtStateMachine<TContext>
    {
        #region ステートクラス本体と特別ステートクラスの定義
        /// <summary>
        /// ステートマシンが処理する状態を表現するステートクラスです。
        /// </summary>
        public abstract class State
        {
            // メンバ変数定義
            internal Dictionary<int, State> transitionTable;
            internal ImtStateMachine<TContext> stateMachine;



            /// <summary>
            /// このステートが所属するステートマシン
            /// </summary>
            protected ImtStateMachine<TContext> StateMachine => stateMachine;


            /// <summary>
            /// このステートが所属するステートマシンが持っているコンテキスト
            /// </summary>
            protected TContext Context => stateMachine.Context;



            /// <summary>
            /// ステートに突入したときの処理を行います
            /// </summary>
            protected internal virtual void Enter()
            {
            }


            /// <summary>
            /// ステートを更新するときの処理を行います
            /// </summary>
            protected internal virtual void Update()
            {
            }


            /// <summary>
            /// ステートから脱出したときの処理を行います
            /// </summary>
            protected internal virtual void Exit()
            {
            }


            /// <summary>
            /// ステートマシンがイベントを受ける時に、このステートがそのイベントをガードします
            /// </summary>
            /// <param name="eventId">渡されたイベントID</param>
            /// <returns>イベントの受付をガードする場合は true を、ガードせずイベントを受け付ける場合は false を返します</returns>
            protected internal virtual bool GuardEvent(int eventId)
            {
                // 通常はガードしない
                return false;
            }


            /// <summary>
            /// ステートマシンがスタックしたステートをポップする前に、このステートがそのポップをガードします
            /// </summary>
            /// <returns>ポップの動作をガードする場合は true を、ガードせずにポップ動作を続ける場合は false を返します</returns>
            protected internal virtual bool GuardPop()
            {
                // 通常はガードしない
                return false;
            }
        }



        /// <summary>
        /// ステートマシンで "任意" を表現する特別なステートクラスです
        /// </summary>
        private sealed class AnyState : State { }
        #endregion



        #region 列挙型定義
        /// <summary>
        /// ステートマシンのUpdate状態を表現します
        /// </summary>
        private enum UpdateState
        {
            /// <summary>
            /// アイドリング中です。つまり何もしていません
            /// </summary>
            Idle,

            /// <summary>
            /// ステートの突入処理中です
            /// </summary>
            Enter,

            /// <summary>
            /// ステートの更新処理中です
            /// </summary>
            Update,

            /// <summary>
            /// ステートの脱出処理中です
            /// </summary>
            Exit,
        }
        #endregion



        #region メンバ変数定義
        // メンバ変数定義
        private UpdateState updateState;
        private List<State> stateList;
        private State currentState;
        private State nextState;
        private Stack<State> stateStack;
        #endregion



        #region プロパティ定義
        /// <summary>
        /// ステートマシンが保持しているコンテキスト
        /// </summary>
        public TContext Context { get; private set; }


        /// <summary>
        /// ステートマシンが起動しているかどうか
        /// </summary>
        public bool Running => currentState != null;


        /// <summary>
        /// ステートマシンが、更新処理中かどうか。
        /// Update 関数から抜けたと思っても、このプロパティが true を示す場合、
        /// Update 中に例外などで不正な終了の仕方をしている場合が考えられます。
        /// </summary>
        public bool Updating => (Running && updateState != UpdateState.Idle);


        /// <summary>
        /// 現在のスタックしているステートの数
        /// </summary>
        public int StackCount => stateStack.Count;
        #endregion



        #region コンストラクタ
        /// <summary>
        /// ImtStateMachine のインスタンスを初期化します
        /// </summary>
        /// <param name="context">このステートマシンが持つコンテキスト</param>
        /// <exception cref="ArgumentNullException">context が null です</exception>
        public ImtStateMachine(TContext context)
        {
            // 渡されたコンテキストがnullなら
            if (context == null)
            {
                // nullは許されない
                throw new ArgumentNullException(nameof(context));
            }


            // メンバの初期化をする
            Context = context;
            stateList = new List<State>();
            stateStack = new Stack<State>();
            updateState = UpdateState.Idle;


            // この時点で任意ステートのインスタンスを作ってしまう
            GetOrCreateState<AnyState>();
        }
        #endregion


        #region ステート遷移テーブル構築系
        /// <summary>
        /// ステートの任意遷移構造を追加します。
        /// </summary>
        /// <remarks>
        /// この関数は、遷移元が任意の状態からの遷移を希望する場合に利用してください。
        /// 任意の遷移は、通常の遷移（Any以外の遷移元）より優先度が低いことにも、注意をしてください。
        /// また、ステートの遷移テーブル設定はステートマシンが起動する前に完了しなければなりません。
        /// </remarks>
        /// <typeparam name="TNextState">任意状態から遷移する先になるステートの型</typeparam>
        /// <param name="eventId">遷移する条件となるイベントID</param>
        /// <exception cref="ArgumentException">既に同じ eventId が設定された遷移先ステートが存在します</exception>
        /// <exception cref="InvalidOperationException">ステートマシンは、既に起動中です</exception>
        public void AddAnyTransition<TNextState>(int eventId) where TNextState : State, new()
        {
            // 単純に遷移元がAnyStateなだけの単純な遷移追加関数を呼ぶ
            AddTransition<AnyState, TNextState>(eventId);
        }


        /// <summary>
        /// ステートの遷移構造を追加します。
        /// また、ステートの遷移テーブル設定はステートマシンが起動する前に完了しなければなりません。
        /// </summary>
        /// <typeparam name="TPrevState">遷移する元になるステートの型</typeparam>
        /// <typeparam name="TNextState">遷移する先になるステートの型</typeparam>
        /// <param name="eventId">遷移する条件となるイベントID</param>
        /// <exception cref="ArgumentException">既に同じ eventId が設定された遷移先ステートが存在します</exception>
        /// <exception cref="InvalidOperationException">ステートマシンは、既に起動中です</exception>
        public void AddTransition<TPrevState, TNextState>(int eventId) where TPrevState : State, new() where TNextState : State, new()
        {
            // ステートマシンが起動してしまっている場合は
            if (Running)
            {
                // もう設定できないので例外を吐く
                throw new InvalidOperationException("ステートマシンは、既に起動中です");
            }


            // 遷移元と遷移先のステートインスタンスを取得
            var prevState = GetOrCreateState<TPrevState>();
            var nextState = GetOrCreateState<TNextState>();


            // 遷移元ステートの遷移テーブルに既に同じイベントIDが存在していたら
            if (prevState.transitionTable.ContainsKey(eventId))
            {
                // 上書き登録を許さないので例外を吐く
                throw new ArgumentException($"ステート'{prevState.GetType().Name}'には、既にイベントID'{eventId}'の遷移が設定済みです");
            }


            // 遷移テーブルに遷移を設定する
            prevState.transitionTable[eventId] = nextState;
        }


        /// <summary>
        /// ステートマシンが起動する時に、最初に開始するステートを設定します。
        /// </summary>
        /// <typeparam name="TStartState">ステートマシンが起動時に開始するステートの型</typeparam>
        /// <exception cref="InvalidOperationException">ステートマシンは、既に起動中です</exception>
        public void SetStartState<TStartState>() where TStartState : State, new()
        {
            // 既にステートマシンが起動してしまっている場合は
            if (Running)
            {
                // 起動してしまったらこの関数の操作は許されない
                throw new InvalidOperationException("ステートマシンは、既に起動中です");
            }


            // 次に処理するステートの設定をする
            nextState = GetOrCreateState<TStartState>();
        }
        #endregion


        #region ステートスタック操作系
        /// <summary>
        /// 現在実行中のステートを、ステートスタックにプッシュします
        /// </summary>
        /// <exception cref="InvalidOperationException">ステートマシンは、まだ起動していません</exception>
        public void PushState()
        {
            // そもそもまだ現在実行中のステートが存在していないなら例外を投げる
            IfNotRunningThrowException();


            // 現在のステートをスタックに積む
            stateStack.Push(currentState);
        }


        /// <summary>
        /// ステートスタックに積まれているステートを取り出し、遷移の準備を行います。
        /// </summary>
        /// <remarks>
        /// この関数の挙動は、イベントIDを送ることのない点を除けば SendEvent 関数と非常に似ています。
        /// 既に SendEvent によって次の遷移の準備ができている場合は、スタックからステートはポップされることはありません。
        /// </remarks>
        /// <returns>スタックからステートがポップされ次の遷移の準備が完了した場合は true を、ポップするステートがなかったり、ステートによりポップがガードされた場合は false を返します</returns>
        /// <exception cref="InvalidOperationException">ステートマシンは、まだ起動していません</exception>
        public virtual bool PopState()
        {
            // そもそもまだ現在実行中のステートが存在していないなら例外を投げる
            IfNotRunningThrowException();


            // そもそもスタックが空であるか、次に遷移するステートが存在するか、ポップする前に現在のステートにガードされたのなら
            if (stateStack.Count == 0 || nextState != null || currentState.GuardPop())
            {
                // ポップ自体出来ないのでfalseを返す
                return false;
            }


            // ステートをスタックから取り出して次のステートへ遷移するようにして成功を返す
            nextState = stateStack.Pop();
            return true;
        }


        /// <summary>
        /// ステートスタックに積まれているステートを一つ取り出し、そのまま捨てます。
        /// </summary>
        /// <remarks>
        /// ステートスタックの一番上に積まれているステートをそのまま捨てたい時に利用します。
        /// </remarks>
        public void PopAndDropState()
        {
            // スタックが空なら
            if (stateStack.Count == 0)
            {
                // 何もせず終了
                return;
            }


            // スタックからステートを取り出して何もせずそのまま捨てる
            stateStack.Pop();
        }


        /// <summary>
        /// ステートスタックに積まれているすべてのステートを捨てます。
        /// </summary>
        public void ClearStack()
        {
            // スタックを空にする
            stateStack.Clear();
        }
        #endregion


        #region ステートマシン制御系
        /// <summary>
        /// 現在実行中のステートが、指定されたステートかどうかを調べます。
        /// </summary>
        /// <typeparam name="TState">確認するステートの型</typeparam>
        /// <returns>指定されたステートの状態であれば true を、異なる場合は false を返します</returns>
        /// <exception cref="InvalidOperationException">ステートマシンは、まだ起動していません</exception>
        public bool IsCurrentState<TState>() where TState : State
        {
            // そもそもまだ現在実行中のステートが存在していないなら例外を投げる
            IfNotRunningThrowException();


            // 現在のステートと型が一致するかの条件式の結果をそのまま返す
            return currentState.GetType() == typeof(TState);
        }


        /// <summary>
        /// ステートマシンにイベントを送信して、ステート遷移の準備を行います。
        /// </summary>
        /// <remarks>
        /// ステートの遷移は直ちに行われず、次の Update が実行された時に遷移処理が行われます。
        /// また、この関数によるイベント受付優先順位は、一番最初に遷移を受け入れたイベントのみであり Update によって遷移されるまで、後続のイベントはすべて失敗します。
        /// さらに、イベントはステートの Enter または Update 処理中でも受け付けることが可能で、ステートマシンの Update 中に
        /// 何度も遷移をすることが可能ですが Exit 中で遷移中になるため例外が送出されます。
        /// </remarks>
        /// <param name="eventId">ステートマシンに送信するイベントID</param>
        /// <returns>ステートマシンが送信されたイベントを受け付けた場合は true を、イベントを拒否または、イベントの受付ができない場合は false を返します</returns>
        /// <exception cref="InvalidOperationException">ステートマシンは、まだ起動していません</exception>
        /// <exception cref="InvalidOperationException">ステートが Exit 処理中のためイベントを受け付けることが出来ません</exception>
        public virtual bool SendEvent(int eventId)
        {
            // そもそもまだ現在実行中のステートが存在していないなら例外を投げる
            IfNotRunningThrowException();


            // もし Exit 処理中なら
            if (updateState == UpdateState.Exit)
            {
                // Exit 中の SendEvent は許されない
                throw new InvalidOperationException("ステートが Exit 処理中のためイベントを受け付けることが出来ません");
            }


            // 既に遷移準備をしているなら
            if (nextState != null)
            {
                // イベントの受付が出来なかったことを返す
                return false;
            }


            // 現在のステートにイベントガードを呼び出して、ガードされたら
            if (currentState.GuardEvent(eventId))
            {
                // ガードされて失敗したことを返す
                return false;
            }


            // 次に遷移するステートを現在のステートから取り出すが見つけられなかったら
            if (!currentState.transitionTable.TryGetValue(eventId, out nextState))
            {
                // 任意ステートからすらも遷移が出来なかったのなら
                if (!GetOrCreateState<AnyState>().transitionTable.TryGetValue(eventId, out nextState))
                {
                    // イベントの受付が出来なかった
                    return false;
                }
            }


            // イベントの受付をした事を返す
            return true;
        }


        /// <summary>
        /// ステートマシンの状態を更新します。
        /// </summary>
        /// <remarks>
        /// ステートマシンの現在処理しているステートの更新を行いますが、まだ未起動の場合は SetStartState 関数によって設定されたステートが起動します。
        /// また、ステートマシンが初回起動時の場合、ステートのUpdateは呼び出されず、次の更新処理が実行される時になります。
        /// </remarks>
        /// <exception cref="InvalidOperationException">現在のステートマシンは、既に更新処理を実行しています</exception>
        /// <exception cref="InvalidOperationException">開始ステートが設定されていないため、ステートマシンの起動が出来ません</exception>
        public virtual void Update()
        {
            // もしステートマシンの更新状態がアイドリング以外だったら
            if (updateState != UpdateState.Idle)
            {
                // 多重でUpdateが呼び出せない例外を吐く
                throw new InvalidOperationException("現在のステートマシンは、既に更新処理を実行しています");
            }


            // まだ未起動なら
            if (!Running)
            {
                // 次に処理するべきステート（つまり起動開始ステート）が未設定なら
                if (nextState == null)
                {
                    // 起動が出来ない例外を吐く
                    throw new InvalidOperationException("開始ステートが設定されていないため、ステートマシンの起動が出来ません");
                }


                // 現在処理中ステートとして設定する
                currentState = nextState;
                nextState = null;


                // Enter処理中であることを設定してEnterを呼ぶ
                updateState = UpdateState.Enter;
                currentState.Enter();


                // 次に遷移するステートが無いなら
                if (nextState == null)
                {
                    // 起動処理は終わったので一旦終わる
                    updateState = UpdateState.Idle;
                    return;
                }
            }


            // 次に遷移するステートが存在していないなら
            if (nextState == null)
            {
                // Update処理中であることを設定してUpdateを呼ぶ
                updateState = UpdateState.Update;
                currentState.Update();
            }


            // 次に遷移するステートが存在している間ループ
            while (nextState != null)
            {
                // Exit処理中であることを設定してExit処理を呼ぶ
                updateState = UpdateState.Exit;
                currentState.Exit();


                // 次のステートに切り替える
                currentState = nextState;
                nextState = null;


                // Enter処理中であることを設定してEnterを呼ぶ
                updateState = UpdateState.Enter;
                currentState.Enter();
            }


            // 更新処理が終わったらアイドリングに戻る
            updateState = UpdateState.Idle;
        }
        #endregion


        #region 内部ロジック系
        /// <summary>
        /// ステートマシンが未起動の場合に例外を送出します
        /// </summary>
        /// <exception cref="InvalidOperationException">ステートマシンは、まだ起動していません</exception>
        protected void IfNotRunningThrowException()
        {
            // そもそもまだ現在実行中のステートが存在していないなら
            if (!Running)
            {
                // まだ起動すらしていないので例外を吐く
                throw new InvalidOperationException("ステートマシンは、まだ起動していません");
            }
        }


        /// <summary>
        /// 指定されたステートの型のインスタンスを取得しますが、存在しない場合は生成してから取得します。
        /// 生成されたインスタンスは、次回から取得されるようになります。
        /// </summary>
        /// <typeparam name="TState">取得、または生成するステートの型</typeparam>
        /// <returns>取得、または生成されたステートのインスタンスを返します</returns>
        private TState GetOrCreateState<TState>() where TState : State, new()
        {
            // 要求ステートの型を取得
            var requestStateType = typeof(TState);


            // ステートの数分回る
            foreach (var state in stateList)
            {
                // もし該当のステートの型と一致するインスタンスなら
                if (state.GetType() == requestStateType)
                {
                    // そのインスタンスを返す
                    return (TState)state;
                }
            }


            // ループから抜けたのなら、型一致するインスタンスが無いという事なのでインスタンスを生成してキャッシュする
            var newState = new TState();
            stateList.Add(newState);


            // 新しいステートに、自身の参照と遷移テーブルのインスタンスの初期化も行って返す
            newState.stateMachine = this;
            newState.transitionTable = new Dictionary<int, State>();
            return newState;
        }
        #endregion
    }



    #region 同期ステートマシンの実装
    #region 同期コンテキストの実装
    /// <summary>
    /// 同期ステートマシンが保持する、同期コンテキストのクラスです
    /// </summary>
    /// <remarks>
    /// このクラスは ImtSynchronizationStateMachine クラスが、ステートで処理される非同期処理を同期的に制御する際に利用されます
    /// </remarks>
    internal class StateMachineSynchronizationContext : SynchronizationContext
    {
        /// <summary>
        /// 同期コンテキストに送られてきたコールバック関数を保持する構造体です
        /// </summary>
        private struct Message
        {
            /// <summary>
            /// 処理するべきコールバック
            /// </summary>
            private SendOrPostCallback callback;

            /// <summary>
            /// コールバック関数を呼ぶときに渡すべき状態オブジェクト
            /// </summary>
            private object state;

            /// <summary>
            /// 非同期処理側からの同期呼び出しが行われた際の待機オブジェクト
            /// </summary>
            private ManualResetEvent waitHandle;



            /// <summary>
            /// Message インスタンスの初期化を行います
            /// </summary>
            /// <param name="callback">呼び出すべきコールバック</param>
            /// <param name="state">コールバックに渡すべきステートオブジェクト</param>
            /// <param name="waitHandle">同期呼び出しの際に必要な待機オブジェクト</param>
            public Message(SendOrPostCallback callback, object state, ManualResetEvent waitHandle)
            {
                // 渡されたものを覚える
                this.callback = callback;
                this.state = state;
                this.waitHandle = waitHandle;
            }


            /// <summary>
            /// メッセージの呼び出しを行います。
            /// また、この時に同期オブジェクトが渡されていた場合は、同期オブジェクトのシグナルも送信します。
            /// </summary>
            public void Invoke()
            {
                // コールバックの呼び出し
                callback(state);


                // もし待機オブジェクトがあるなら
                if (waitHandle != null)
                {
                    // シグナルを送る
                    waitHandle.Set();
                }
            }
        }



        // 以下メンバ変数定義
        private int myStartupThreadId;
        private Queue<Message> messageQueue;



        /// <summary>
        /// StateMachineSynchronizationContext のインスタンスを初期化します
        /// </summary>
        public StateMachineSynchronizationContext()
        {
            // 自分が起動したスレッドのIDを覚えつつ
            // ポストされたメッセージを溜めるキューのインスタンスを生成
            // （ステート内でのみのメッセージなので容量は既定値のまま）
            myStartupThreadId = Thread.CurrentThread.ManagedThreadId;
            messageQueue = new Queue<Message>();
        }


        /// <summary>
        /// 同期コンテキストに、メッセージを同期呼び出しするように送信します
        /// </summary>
        /// <param name="callback">同期呼び出しをして欲しいコールバック関数</param>
        /// <param name="state">コールバックに渡すオブジェクト</param>
        public override void Send(SendOrPostCallback callback, object state)
        {
            // 呼び出し側が同じスレッドなら
            if (Thread.CurrentThread.ManagedThreadId == myStartupThreadId)
            {
                // わざわざ同期コンテキストに送らないで、自分で呼んでや
                callback(state);
                return;
            }


            // 待機オブジェクトを作って
            using (var waitHandle = new ManualResetEvent(false))
            {
                // キューをロック
                lock (messageQueue)
                {
                    // キューに自分のメッセージが処理されるようにキューイング
                    messageQueue.Enqueue(new Message(callback, state, waitHandle));
                }


                // メッセージキューから取り出されて処理されるまで待つ
                waitHandle.WaitOne();
            }
        }


        /// <summary>
        /// 同期コンテキストに、メッセージを非同期的に呼び出すようにポストします
        /// </summary>
        /// <param name="callback">ポストするコールバック関数</param>
        /// <param name="state">ポストしたコールバック関数に渡すオブジェクト</param>
        public override void Post(SendOrPostCallback callback, object state)
        {
            // キューをロック
            lock (messageQueue)
            {
                // キューにメッセージが処理されるようにキューイング
                messageQueue.Enqueue(new Message(callback, state, null));
            }
        }


        /// <summary>
        /// メッセージキューに蓄えられている、メッセージをすべて処理します
        /// </summary>
        internal void DoProcessMessages()
        {
            // キューをロック
            lock (messageQueue)
            {
                // キューが空になるまでループ
                while (messageQueue.Count > 0)
                {
                    // キューからメッセージを取り出してそのまま呼び出しを行う
                    messageQueue.Dequeue().Invoke();
                }
            }
        }
    }
    #endregion



    #region 同期ステートマシン本体の実装
    /// <summary>
    /// 同期コンテキストの機能を持った、同期ステートマシンクラスです。
    /// </summary>
    /// <remarks>
    /// 同期ステートマシンは、ステートマシンのあらゆる状態制御処理において、同期コンテキストがそのステートマシンに
    /// スイッチし、同期コンテキストのハンドリングは、このステートマシンによって委ねられます。
    /// 状態制御が完了した時は、本来の同期コンテキストに戻ります。
    /// </remarks>
    /// <typeparam name="TContext">このステートマシンが持つコンテキストの型（同期コンテキストの型ではありません）</typeparam>
    public class ImtSynchronizationStateMachine<TContext> : ImtStateMachine<TContext>
    {
        // 以下メンバ変数定義
        private StateMachineSynchronizationContext synchronizationContext;



        /// <summary>
        /// ImtSynchronizationStateMachine のインスタンスを初期化します
        /// </summary>
        /// <param name="context">このステートマシンが持つコンテキスト</param>
        /// <exception cref="ArgumentNullException">context が null です</exception>
        public ImtSynchronizationStateMachine(TContext context) : base(context)
        {
            // ステートマシン用同期コンテキストを生成
            synchronizationContext = new StateMachineSynchronizationContext();
        }


        #region 同期コンテキストのハンドリング系
        /// <summary>
        /// このステートマシンに同期コンテキストをスイッチしてから、このステートマシンに送られた、同期コンテキストのメッセージをすべて処理します。
        /// 操作が完了した時は、本来の同期コンテキストに戻ります。
        /// </summary>
        /// <remarks>
        /// このステートマシンの状態更新処理中に、同期コンテキストに送られたメッセージを処理するためには必ずこの関数を呼び出すようにして下さい。
        /// 呼び出さずに放置をしてしまった場合は、システムに深刻なダメージを与えることになります。
        /// </remarks>
        /// <exception cref="InvalidOperationException">ステートマシンは、まだ起動していません</exception>
        /// <exception cref="InvalidOperationException">現在のステートマシンは、既に更新処理を実行しています</exception>
        public void DoProcessMessage()
        {
            // ステートマシンがまだ起動していないなら例外を吐く
            IfNotRunningThrowException();


            // ステートマシンが既に更新処理を実行しているのなら
            if (Updating)
            {
                // 更新処理中に呼び出すことは許されない
                throw new InvalidOperationException("現在のステートマシンは、既に更新処理を実行しています");
            }


            // もとに戻すために現在の同期コンテキストを拾ってから自身の同期コンテキストに切り替える
            var previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);


            // 同期コンテキストのメッセージを処理する
            synchronizationContext.DoProcessMessages();


            // 本来の同期コンテキストに戻す
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }


        /// <summary>
        /// このステートマシンに同期コンテキストをスイッチしてから、このステートマシンに送られた、同期コンテキストのメッセージをすべて処理します。
        /// さらに、すべてのメッセージの処理が終わったらそのまま更新処理を呼び出します。
        /// 操作が完了した時は、本来の同期コンテキストに戻ります。
        /// </summary>
        /// <remarks>
        /// このステートマシンの状態更新処理中に、同期コンテキストに送られたメッセージを処理するためには必ずこの関数を呼び出すようにして下さい。
        /// 呼び出さずに放置をしてしまった場合は、システムに深刻なダメージを与えることになります。
        /// </remarks>
        /// <exception cref="InvalidOperationException">ステートマシンは、まだ起動していません</exception>
        /// <exception cref="InvalidOperationException">現在のステートマシンは、既に更新処理を実行しています</exception>
        public void DoProcessMessageWithUpdate()
        {
            // ステートマシンがまだ起動していないなら例外を吐く
            IfNotRunningThrowException();


            // ステートマシンが既に更新処理を実行しているのなら
            if (Updating)
            {
                // 更新処理中に呼び出すことは許されない
                throw new InvalidOperationException("現在のステートマシンは、既に更新処理を実行しています");
            }


            // もとに戻すために現在の同期コンテキストを拾ってから自身の同期コンテキストに切り替える
            var previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);


            // 同期コンテキストのメッセージを処理を行い、そのまま継続してUpdateを呼ぶ
            synchronizationContext.DoProcessMessages();
            base.Update();


            // 本来の同期コンテキストに戻す
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
        #endregion


        #region ステートスタック操作系のオーバーライド
        /// <summary>
        /// このステートマシンに同期コンテキストをスイッチしてから、ステートスタックに積まれているステートを取り出し、遷移の準備を行います。
        /// 操作が完了した時は、本来の同期コンテキストに戻ります。
        /// </summary>
        /// <remarks>
        /// この関数の挙動は、イベントIDを送ることのない点を除けば SendEvent 関数と非常に似ています。
        /// 既に SendEvent によって次の遷移の準備ができている場合は、スタックからステートはポップされることはありません。
        /// さらに、この状態更新処理中に呼び出された非同期処理を継続するためには DoProcessMessage 関数を呼び出して下さい。
        /// </remarks>
        /// <returns>スタックからステートがポップされ次の遷移の準備が完了した場合は true を、ポップするステートがなかったり、ステートによりポップがガードされた場合は false を返します</returns>
        /// <exception cref="InvalidOperationException">ステートマシンは、まだ起動していません</exception>
        /// <see cref="DoProcessMessage"/>
        /// <see cref="DoProcessMessageWithUpdate"/>
        public override bool PopState()
        {
            // もとに戻すために現在の同期コンテキストを拾ってから自身の同期コンテキストに切り替える
            var previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);


            // 通常のPopStateを呼び出す
            var result = base.PopState();


            // 本来の同期コンテキストに戻して結果を返す
            SynchronizationContext.SetSynchronizationContext(previousContext);
            return result;
        }
        #endregion


        #region ステートマシン制御系のオーバーライド
        /// <summary>
        /// このステートマシンに同期コンテキストをスイッチしてから、イベントを送信して、ステート遷移の準備を行います。
        /// 操作が完了した時は、本来の同期コンテキストに戻ります。
        /// </summary>
        /// <remarks>
        /// ステートの遷移は直ちに行われず、次の Update が実行された時に遷移処理が行われます。
        /// また、この関数によるイベント受付優先順位は、一番最初に遷移を受け入れたイベントのみであり Update によって遷移されるまで、後続のイベントはすべて失敗します。
        /// さらに、イベントはステートの Enter または Update 処理中でも受け付けることが可能で、ステートマシンの Update 中に
        /// 何度も遷移をすることが可能ですが Exit 中で遷移中になるため例外が送出されます。
        /// そして、この関数の処理中に呼び出された非同期処理を継続するためには DoProcessMessage 関数を呼び出して下さい。
        /// </remarks>
        /// <param name="eventId">ステートマシンに送信するイベントID</param>
        /// <returns>ステートマシンが送信されたイベントを受け付けた場合は true を、イベントを拒否または、イベントの受付ができない場合は false を返します</returns>
        /// <exception cref="InvalidOperationException">ステートマシンは、まだ起動していません</exception>
        /// <exception cref="InvalidOperationException">ステートが Exit 処理中のためイベントを受け付けることが出来ません</exception>
        /// <see cref="DoProcessMessage"/>
        /// <see cref="DoProcessMessageWithUpdate"/>
        public override bool SendEvent(int eventId)
        {
            // もとに戻すために現在の同期コンテキストを拾ってから自身の同期コンテキストに切り替える
            var previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);


            // 通常のSendEventを呼び出す
            var result = base.SendEvent(eventId);


            // 本来の同期コンテキストに戻して結果を返す
            SynchronizationContext.SetSynchronizationContext(previousContext);
            return result;
        }


        /// <summary>
        /// このステートマシンに同期コンテキストをスイッチしてから、状態を更新します。
        /// 操作が完了した時は、本来の同期コンテキストに戻ります。
        /// </summary>
        /// <remarks>
        /// ステートマシンの現在処理しているステートの更新を行いますが、まだ未起動の場合は SetStartState 関数によって設定されたステートが起動します。
        /// また、ステートマシンが初回起動時の場合、ステートのUpdateは呼び出されず、次の更新処理が実行される時になります。
        /// さらに、この状態更新処理中に呼び出された非同期処理を継続するためには DoProcessMessage 関数を呼び出して下さい。
        /// </remarks>
        /// <exception cref="InvalidOperationException">現在のステートマシンは、既に更新処理を実行しています</exception>
        /// <exception cref="InvalidOperationException">開始ステートが設定されていないため、ステートマシンの起動が出来ません</exception>
        /// <see cref="DoProcessMessage"/>
        /// <see cref="DoProcessMessageWithUpdate"/>
        public override void Update()
        {
            // もとに戻すために現在の同期コンテキストを拾ってから自身の同期コンテキストに切り替える
            var previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);


            // 通常のUpdateを呼び出す
            base.Update();


            // 本来の同期コンテキストに戻す
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
        #endregion
    }
    #endregion
    #endregion
}