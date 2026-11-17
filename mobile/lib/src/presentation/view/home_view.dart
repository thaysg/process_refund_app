import 'package:flutter/material.dart';

import '../widgets/desktop_layout.dart';
import '../widgets/input_card.dart';
import '../widgets/mobile_layout.dart';
import '../widgets/tablet_layout.dart';

class HomeView extends StatelessWidget {
  const HomeView({
    super.key,
    required this.fileName,
    required this.referenceDateLabel,
    required this.loading,
    required this.onPickFile,
    required this.onPickDate,
    required this.onProcess,
    required this.onRetry,
  });

  final String? fileName;
  final String referenceDateLabel;
  final bool loading;
  final VoidCallback onPickFile;
  final VoidCallback onPickDate;
  final VoidCallback onProcess;
  final VoidCallback onRetry;

  static const double mobileBreakpoint = 600;
  static const double tabletBreakpoint = 1024;

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: LayoutBuilder(
        builder: (context, constraints) {
          final width = constraints.maxWidth;
          final isMobile = width < mobileBreakpoint;
          final isTablet =
              width >= mobileBreakpoint && width < tabletBreakpoint;

          final padding = EdgeInsets.all(isMobile ? 16 : 24);
          final maxContentWidth = isMobile
              ? double.infinity
              : (isTablet ? 900.0 : 1200.0);

          final input = InputCard(
            fileName: fileName,
            referenceDateLabel: referenceDateLabel,
            loading: loading,
            onPickFile: onPickFile,
            onPickDate: onPickDate,
            onProcess: onProcess,
          );

          Widget content;
          if (isMobile) {
            content = MobileLayout(input: input, onRetry: onRetry);
          } else if (isTablet) {
            content = TabletLayout(input: input, onRetry: onRetry);
          } else {
            content = DesktopLayout(input: input, onRetry: onRetry);
          }

          return Align(
            alignment: Alignment.topCenter,
            child: ConstrainedBox(
              constraints: BoxConstraints(maxWidth: maxContentWidth),
              child: Padding(
                padding: padding,
                child: content,
              ),
            ),
          );
        },
      ),
    );
  }
}
