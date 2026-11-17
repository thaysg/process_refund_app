import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'result_slivers.dart';

class MobileLayout extends ConsumerWidget {
  const MobileLayout({super.key, required this.input, required this.onRetry});

  final Widget input;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return CustomScrollView(
      physics: const BouncingScrollPhysics(),
      slivers: [
        SliverToBoxAdapter(child: input),
        const SliverToBoxAdapter(child: SizedBox(height: 16)),
        ...buildResultSlivers(context, ref, onRetry: onRetry),
      ],
    );
  }
}
