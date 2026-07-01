import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../auth/auth_notifier.dart';
import '../../auth/auth_state.dart';
import 'login_screen.dart';

class SignupScreen extends StatefulWidget {
  const SignupScreen({super.key, required this.auth});

  final AuthNotifier auth;

  @override
  State<SignupScreen> createState() => _SignupScreenState();
}

class _SignupScreenState extends State<SignupScreen> {
  final _formKey = GlobalKey<FormState>();
  final _name = TextEditingController();
  final _email = TextEditingController();
  final _password = TextEditingController();

  @override
  void dispose() {
    _name.dispose();
    _email.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    await widget.auth.register(
      _email.text.trim(),
      _password.text,
      _name.text.trim(),
    );
  }

  @override
  Widget build(BuildContext context) {
    return AuthScaffold(
      title: 'Create your account',
      subtitle: 'Student accounts can browse and join published events.',
      child: AnimatedBuilder(
        animation: widget.auth,
        builder: (context, _) {
          final busy = widget.auth.state.status == AuthStatus.busy;
          return Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                TextFormField(
                  controller: _name,
                  textCapitalization: TextCapitalization.words,
                  decoration: const InputDecoration(
                    labelText: 'Display name',
                    prefixIcon: Icon(Icons.badge_rounded),
                  ),
                  validator: (value) =>
                      (value ?? '').trim().isEmpty ? 'Enter your name.' : null,
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: _email,
                  keyboardType: TextInputType.emailAddress,
                  decoration: const InputDecoration(
                    labelText: 'Email',
                    prefixIcon: Icon(Icons.alternate_email_rounded),
                  ),
                  validator: (value) => (value ?? '').contains('@')
                      ? null
                      : 'Enter a valid email.',
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: _password,
                  obscureText: true,
                  decoration: const InputDecoration(
                    labelText: 'Password',
                    prefixIcon: Icon(Icons.lock_rounded),
                  ),
                  validator: (value) => (value ?? '').length >= 6
                      ? null
                      : 'Use at least 6 characters.',
                ),
                if (widget.auth.state.errorMessage != null) ...[
                  const SizedBox(height: 12),
                  Text(
                    widget.auth.state.errorMessage!,
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
                ],
                const SizedBox(height: 20),
                FilledButton.icon(
                  onPressed: busy ? null : _submit,
                  icon: busy
                      ? const SizedBox.square(
                          dimension: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.person_add_alt_1_rounded),
                  label: const Text('Create account'),
                ),
                TextButton(
                  onPressed: busy ? null : () => context.go('/login'),
                  child: const Text('I already have an account'),
                ),
              ],
            ),
          );
        },
      ),
    );
  }
}
