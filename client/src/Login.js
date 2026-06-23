import './SignUp.css';
import { BsEnvelope } from 'react-icons/bs';
import { RiLockPasswordLine } from 'react-icons/ri';

const Login = ({ onSwitchToSignUp }) => {
  return (
    <div className="auth">
      <div className="auth-card">
        <h2>Log in</h2>
        <p>Enter your email and password to access your account.</p>
        <form className="auth-form">
          <label htmlFor="email"><BsEnvelope style={{ marginRight: '8px' }} />Email</label>
          <input id="email" type="email" placeholder="you@example.com" />
          <label htmlFor="password"><RiLockPasswordLine style={{ marginRight: '8px' }} />Password</label>
          <input id="password" type="password" placeholder="Enter your password" />
          <button type="submit">Login</button>
        </form>
        <p>
          Don't have an account?{' '}
          <button type="button" className="link-button" onClick={onSwitchToSignUp}>
            Sign up
          </button>
        </p>
      </div>
    </div>
  );
}
 
export default Login;