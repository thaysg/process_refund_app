import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'result_slivers.dart';

class DesktopLayout extends ConsumerWidget {
  const DesktopLayout({super.key, required this.input, required this.onRetry});

  final Widget input;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(width: 380, child: SingleChildScrollView(child: input)),
        const SizedBox(width: 24),
        Expanded(
          child: CustomScrollView(
            physics: const BouncingScrollPhysics(),
            slivers: buildResultSlivers(context, ref, onRetry: onRetry),
          ),
        ),
      ],
    );
  }
}
