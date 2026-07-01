class AppUser {
  const AppUser({
    required this.userId,
    required this.email,
    required this.displayName,
    required this.role,
    this.createdAt,
  });

  final String userId;
  final String email;
  final String displayName;
  final String role;
  final DateTime? createdAt;

  String get normalizedRole => role.trim().toLowerCase();

  String? get firstName {
    final parts = displayName.trim().split(' ');
    return parts.isNotEmpty ? parts.first : null;
  }

  factory AppUser.fromJson(Map<String, dynamic> json) => AppUser(
    userId: json['userId'] as String,
    email: json['email'] as String,
    displayName: json['displayName'] as String,
    role: json['role'] as String,
    createdAt: json['createdAt'] == null
        ? null
        : DateTime.parse(json['createdAt'] as String).toLocal(),
  );
}
