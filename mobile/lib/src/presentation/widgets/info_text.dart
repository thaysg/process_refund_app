import 'package:flutter/material.dart';

class InfoText extends StatelessWidget {
  const InfoText({super.key, required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Text(
      '$label: $value',
      style: Theme.of(context).textTheme.bodyMedium,
    );
  }
}
