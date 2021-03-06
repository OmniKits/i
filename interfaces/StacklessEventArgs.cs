﻿using System;
using System.Linq;
using System.Threading;

using Context = System.Threading.SynchronizationContext;

public abstract class StacklessEventArgs<T> : EventArgs
    where T : StacklessEventArgs<T>
{
    class ExContext
    {
        public Context Context;
        public EventHandler<T> Handler;
        public object Sender;

        public ExContext(Context context, EventHandler<T> handler, object sender)
        {
            Context = context;
            Handler = handler;
            Sender = sender;
        }
    }

    private ExContext _Context;

    public bool Done()
    {
        var xContext = _Context;
        if (xContext == null)
            return true;

        if (xContext.Context != Context.Current)
            throw new InvalidOperationException();

#if UseInterlocked
        return Interlocked.CompareExchange(ref _Context, null, xContext) == xContext;
#else
        _Context = null;
        return false;
#endif
    }

    public void Next()
    {
        var xContext = _Context;
        if (xContext == null)
            return;

        var context = xContext.Context;
        if (context != Context.Current)
            throw new InvalidOperationException();

        var handler = xContext.Handler;
        var sender = xContext.Sender;

        var current = (EventHandler<T>)handler.GetInvocationList().Last();

        var remains = handler - current;
        var @new = (remains == null) ? null : new ExContext(context, remains, sender);

#if UseInterlocked
        if (Interlocked.CompareExchange(ref _Context, @new, xContext) != xContext)
            throw new InvalidOperationException();
#else
        _Context = @new;
#endif

        current(sender, (T)this);

        Next();
    }

    public void Invoke(EventHandler<T> handler, object sender)
    {
        var @new = new ExContext(Context.Current, handler, sender);
        if (Interlocked.CompareExchange(ref _Context, @new, null) != null)
            throw new InvalidOperationException();

        Next();
    }
}
