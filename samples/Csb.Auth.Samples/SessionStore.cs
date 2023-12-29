using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Csb.Auth.Samples
{
    public class SessionStore
    {
        private readonly ConcurrentBag<Session> _sessions = new ConcurrentBag<Session>();

        public void Logout(Session session) => _sessions.Add(session);

        public bool IsLoggedOut(Session session) => _sessions.Contains(session);
    }
}
