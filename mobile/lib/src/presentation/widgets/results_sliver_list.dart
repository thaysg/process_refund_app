import 'package:flutter/material.dart';

import '../../domain/models.dart';
import 'result_tile.dart';

class ResultsSliverList extends StatelessWidget {
  const ResultsSliverList({super.key, required this.items});

  final List<ReimbursementItem> items;

  @override
  Widget build(BuildContext context) {
    return SliverList.builder(
      itemCount: items.length,
      itemBuilder: (context, index) => Padding(
        padding: EdgeInsets.only(bottom: index == items.length - 1 ? 0 : 8),
        child: ResultTile(item: items[index]),
      ),
    );
  }
}
