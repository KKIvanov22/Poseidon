import React, { useState } from 'react';
import Login from './Login';
import SignUp from './SignUp';

function App() {
  const [view, setView] = useState('login');

  return (
    <div className="App">
      <div className="content">
        {view === 'login' ? (
          <Login onSwitchToSignUp={() => setView('signup')} />
        ) : (
          <SignUp onSwitchToLogin={() => setView('login')} />
        )}
      </div>
    </div>
  );
}

export default App;
