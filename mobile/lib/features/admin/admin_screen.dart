import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../auth/auth_notifier.dart';
import '../../core/api_endpoints.dart';
import '../../core/api_exception.dart';
import '../../models/event.dart';
import '../../models/notification_job.dart';
import '../../models/user.dart';
import '../../widgets/app_chrome.dart';
import '../../widgets/empty_state.dart';
import '../../widgets/event_card.dart';

class AdminScreen extends StatefulWidget {
  const AdminScreen({super.key, required this.auth});

  final AuthNotifier auth;

  @override
  State<AdminScreen> createState() => _AdminScreenState();
}

class _AdminScreenState extends State<AdminScreen> {
  var _index = 0;
  var _users = <AppUser>[];
  var _events = <Event>[];
  var _jobs = <NotificationJob>[];
  var _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final token = widget.auth.token;
    if (token == null) return;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([
        apiListUsers(token),
        apiGetEvents(token),
        apiGetPendingJobs(token),
      ]);
      _users = results[0]
          .map((item) => AppUser.fromJson(item as Map<String, dynamic>))
          .toList();
      _events = results[1]
          .map((item) => Event.fromJson(item as Map<String, dynamic>))
          .toList();
      _jobs = results[2]
          .map((item) => NotificationJob.fromJson(item as Map<String, dynamic>))
          .toList();
    } on ApiException catch (error) {
      _error = error.message;
    } catch (_) {
      _error = 'Unable to load admin data.';
    }
    if (mounted) setState(() => _loading = false);
  }

  Future<void> _run(Future<void> Function(String token) action) async {
    final token = widget.auth.token;
    if (token == null) return;
    try {
      await action(token);
      await _load();
    } on ApiException catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(error.message)));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return AppChrome(
      auth: widget.auth,
      title: 'Admin',
      subtitle: 'Users, approvals, and notifications',
      selectedIndex: _index,
      onDestinationSelected: (value) => setState(() => _index = value),
      destinations: const [
        NavigationDestination(
          icon: Icon(Icons.people_outline_rounded),
          selectedIcon: Icon(Icons.people_rounded),
          label: 'Users',
        ),
        NavigationDestination(
          icon: Icon(Icons.event_note_outlined),
          selectedIcon: Icon(Icons.event_note_rounded),
          label: 'Events',
        ),
        NavigationDestination(
          icon: Icon(Icons.notifications_none_rounded),
          selectedIcon: Icon(Icons.notifications_rounded),
          label: 'Jobs',
        ),
      ],
      child: RefreshIndicator(onRefresh: _load, child: _body()),
    );
  }

  Widget _body() {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error != null) {
      return ListView(
        children: [
          EmptyState(
            icon: Icons.cloud_off_rounded,
            title: "Couldn't load admin panel",
            message: _error!,
          ),
        ],
      );
    }
    return switch (_index) {
      0 => _usersTab(),
      1 => _eventsTab(),
      _ => _jobsTab(),
    };
  }

  Widget _usersTab() {
    if (_users.isEmpty) {
      return const EmptyState(
        icon: Icons.people_outline_rounded,
        title: 'No users',
        message: 'Users will appear here after registration.',
      );
    }
    return ListView.separated(
      padding: const EdgeInsets.all(16),
      itemCount: _users.length,
      separatorBuilder: (_, _) => const SizedBox(height: 8),
      itemBuilder: (context, index) {
        final user = _users[index];
        final isAdmin = user.normalizedRole == 'admin';
        return Card(
          child: ListTile(
            leading: CircleAvatar(
              child: Text(user.displayName.characters.first),
            ),
            title: Text(user.displayName),
            subtitle: Text('${user.email}\n${user.role}'),
            isThreeLine: true,
            trailing: isAdmin
                ? null
                : PopupMenuButton<String>(
                    tooltip: 'Change role',
                    onSelected: (role) => _run(
                      (token) async =>
                          apiUpdateUserRole(token, user.userId, role),
                    ),
                    itemBuilder: (context) => const [
                      PopupMenuItem(value: 'Student', child: Text('Student')),
                      PopupMenuItem(value: 'Teacher', child: Text('Teacher')),
                    ],
                  ),
          ),
        );
      },
    );
  }

  Widget _eventsTab() {
    if (_events.isEmpty) {
      return const EmptyState(
        icon: Icons.event_busy_rounded,
        title: 'No events',
        message: 'Created events will appear here.',
      );
    }
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        ..._events.map(
          (event) => Padding(
            padding: const EdgeInsets.only(bottom: 12),
            child: EventCard(
              event: event,
              trailing: Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  if (event.isDraft)
                    FilledButton.icon(
                      onPressed: () => _run(
                        (token) async => apiPublishEvent(token, event.eventId),
                      ),
                      icon: const Icon(Icons.publish_rounded),
                      label: const Text('Publish'),
                    ),
                  if (!event.isCanceled)
                    OutlinedButton.icon(
                      onPressed: () => _run(
                        (token) async => apiCancelEvent(token, event.eventId),
                      ),
                      icon: const Icon(Icons.cancel_rounded),
                      label: const Text('Cancel'),
                    ),
                ],
              ),
            ),
          ),
        ),
      ],
    );
  }

  Widget _jobsTab() {
    if (_jobs.isEmpty) {
      return const EmptyState(
        icon: Icons.mark_email_read_outlined,
        title: 'No pending jobs',
        message: 'Notification work queue is clear.',
      );
    }
    return ListView.separated(
      padding: const EdgeInsets.all(16),
      itemCount: _jobs.length,
      separatorBuilder: (_, _) => const SizedBox(height: 8),
      itemBuilder: (context, index) {
        final job = _jobs[index];
        return Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  job.title,
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 6),
                Text(job.message),
                const SizedBox(height: 10),
                Text(
                  '${job.channel} · attempts ${job.attempts} · ${DateFormat('MMM d, HH:mm').format(job.availableAt)}',
                  style: Theme.of(context).textTheme.bodySmall,
                ),
                const SizedBox(height: 12),
                Wrap(
                  spacing: 8,
                  children: [
                    FilledButton.icon(
                      onPressed: () => _run(
                        (token) async =>
                            apiCompleteJob(token, job.notificationJobId),
                      ),
                      icon: const Icon(Icons.check_rounded),
                      label: const Text('Complete'),
                    ),
                    OutlinedButton.icon(
                      onPressed: () => _run(
                        (token) async =>
                            apiRetryJob(token, job.notificationJobId),
                      ),
                      icon: const Icon(Icons.refresh_rounded),
                      label: const Text('Retry'),
                    ),
                  ],
                ),
              ],
            ),
          ),
        );
      },
    );
  }
}
