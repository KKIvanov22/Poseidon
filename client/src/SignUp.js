import './SignUp.css';
import { BsEnvelope } from 'react-icons/bs';
import { RiLockPasswordLine } from 'react-icons/ri';
import { FaUser } from 'react-icons/fa';


const SignUp = ({ onSwitchToLogin }) => {
  const handleSubmit = (event) => {
    event.preventDefault();
    //submit form data to backend or perform validation here
  };

  return (
    <div className="auth">
      <div className="auth-card">
        <h2>Sign up</h2>
        <p>Create your account to get started with our app.</p>
        <form className="auth-form" onSubmit={handleSubmit}>
          <label htmlFor="name"><FaUser style={{ marginRight: '8px' }} />Full name</label>
          <input id="name" type="text" placeholder="Your full name" />
          <label htmlFor="email"><BsEnvelope style={{ marginRight: '8px' }} />Email</label>
          <input id="email" type="email" placeholder="you@example.com" />
          <label htmlFor="password"><RiLockPasswordLine style={{ marginRight: '8px' }} />Password</label>
          <input id="password" type="password" placeholder="Create a password" />
          <label htmlFor="confirmPassword">Confirm password</label>
          <input id="confirmPassword" type="password" placeholder="Repeat your password" />
          <button type="submit">Sign Up</button>
        </form>
        <p>
          Already have an account?{' '}
          <button type="button" className="link-button" onClick={onSwitchToLogin}>
            Log in
          </button>
        </p>
      </div>
    </div>
  );
}
 
export default SignUp;