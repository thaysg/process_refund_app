import 'package:flutter/material.dart';

class SummaryChip extends StatelessWidget {
  const SummaryChip({
    super.key,
    required this.label,
    required this.value,
    required this.icon,
  });

  final String label;
  final String value;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: '$label: $value',
      child: Chip(
        avatar: Icon(icon, size: 18),
        label: Text('$label: $value'),
        materialTapTargetSize: MaterialTapTargetSize.padded,
      ),
    );
  }
}
