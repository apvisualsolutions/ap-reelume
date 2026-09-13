// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Identification;

namespace ApSolutions.LocalMedia.Application.Identification;

public sealed record ReviewInboxChanged(
    MediaFileId MediaFileId,
    CandidateId CandidateId,
    ReviewState ReviewState,
    int Revision);
