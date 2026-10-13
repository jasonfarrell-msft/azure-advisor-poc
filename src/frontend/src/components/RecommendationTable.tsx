import React, { useState, useMemo } from 'react';
import { RecommendationCard, RecommendationCategory, RiskLevel, RecommendationStatus } from '../types';
import { Search, ArrowUpRight, Calendar, Trash2, Cpu, Clock, CheckCircle2, AlertCircle, Sparkles } from 'lucide-react';

interface RecommendationTableProps {
  recommendations: RecommendationCard[];
  onSelectRecommendation: (rec: RecommendationCard) => void;
  onOpenSchedulePlanner: (rec: RecommendationCard) => void;
}

export const RecommendationTable: React.FC<RecommendationTableProps> = ({
  recommendations,
  onSelectRecommendation,
  onOpenSchedulePlanner
}) => {
  const [search, setSearch] = useState('');
  const [selectedCategory, setSelectedCategory] = useState<string>('All');
  const [selectedRisk, setSelectedRisk] = useState<string>('All');
  const [selectedStatus, setSelectedStatus] = useState<string>('All');

  const filtered = useMemo(() => {
    return recommendations.filter((r) => {
      const matchesSearch =
        r.resourceName.toLowerCase().includes(search.toLowerCase()) ||
        r.title.toLowerCase().includes(search.toLowerCase()) ||
        r.resourceGroup.toLowerCase().includes(search.toLowerCase()) ||
        (r.targetSku && r.targetSku.toLowerCase().includes(search.toLowerCase()));

      const matchesCat = selectedCategory === 'All' || r.category === selectedCategory;
      const matchesRisk = selectedRisk === 'All' || r.risk === selectedRisk;
      const matchesStatus = selectedStatus === 'All' || r.status === selectedStatus;

      return matchesSearch && matchesCat && matchesRisk && matchesStatus;
    });
  }, [recommendations, search, selectedCategory, selectedRisk, selectedStatus]);

  const getCategoryBadge = (cat: RecommendationCategory) => {
    switch (cat) {
      case 'Rightsizing':
        return (
          <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-blue-100 text-blue-800">
            <Cpu className="w-3 h-3 mr-1" />
            Rightsizing
          </span>
        );
      case 'Scheduling':
        return (
          <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-purple-100 text-purple-800">
            <Clock className="w-3 h-3 mr-1" />
            Scheduling
          </span>
        );
      case 'Removal':
        return (
          <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-rose-100 text-rose-800">
            <Trash2 className="w-3 h-3 mr-1" />
            Removal
          </span>
        );
    }
  };

  const getRiskBadge = (risk: RiskLevel) => {
    switch (risk) {
      case 'Safe':
        return <span className="text-xs px-2 py-0.5 rounded-full font-medium bg-emerald-100 text-emerald-800">Safe</span>;
      case 'Low':
        return <span className="text-xs px-2 py-0.5 rounded-full font-medium bg-blue-100 text-blue-800">Low Risk</span>;
      case 'Medium':
        return <span className="text-xs px-2 py-0.5 rounded-full font-medium bg-amber-100 text-amber-800">Med Risk</span>;
      case 'High':
        return <span className="text-xs px-2 py-0.5 rounded-full font-medium bg-red-100 text-red-800">High Risk</span>;
    }
  };

  const getStatusBadge = (status: RecommendationStatus, exclusionReason?: string) => {
    if (status === 'Actionable') {
      return (
        <span className="inline-flex items-center text-xs px-2 py-0.5 rounded-full font-medium bg-emerald-50 text-emerald-700 border border-emerald-200">
          <CheckCircle2 className="w-3 h-3 mr-1 text-emerald-600" />
          Actionable
        </span>
      );
    }
    return (
      <span
        title={exclusionReason || 'Double-counting prevented by conflict resolver'}
        className="inline-flex items-center text-xs px-2 py-0.5 rounded-full font-medium bg-amber-50 text-amber-800 border border-amber-300 cursor-help"
      >
        <AlertCircle className="w-3 h-3 mr-1 text-amber-600" />
        Overlapped
      </span>
    );
  };

  return (
    <div className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
      {/* Controls & Filter Bar */}
      <div className="p-4 border-b border-slate-200 bg-slate-50/50 flex flex-col md:flex-row md:items-center justify-between gap-3">
        {/* Search */}
        <div className="relative flex-1 max-w-md">
          <Search className="w-4 h-4 text-slate-400 absolute left-3 top-2.5" />
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search by resource, group, or SKU..."
            className="w-full bg-white border border-slate-300 text-slate-800 text-xs rounded-lg pl-9 pr-4 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>

        {/* Filter Dropdowns */}
        <div className="flex flex-wrap items-center gap-2">
          {/* Category */}
          <select
            value={selectedCategory}
            onChange={(e) => setSelectedCategory(e.target.value)}
            className="bg-white border border-slate-300 text-slate-700 text-xs rounded-lg px-2.5 py-1.5 focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="All">All Categories</option>
            <option value="Rightsizing">Rightsizing</option>
            <option value="Scheduling">Scheduling</option>
            <option value="Removal">Removal</option>
          </select>

          {/* Risk */}
          <select
            value={selectedRisk}
            onChange={(e) => setSelectedRisk(e.target.value)}
            className="bg-white border border-slate-300 text-slate-700 text-xs rounded-lg px-2.5 py-1.5 focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="All">All Risks</option>
            <option value="Safe">Safe</option>
            <option value="Low">Low</option>
            <option value="Medium">Medium</option>
            <option value="High">High</option>
          </select>

          {/* Status */}
          <select
            value={selectedStatus}
            onChange={(e) => setSelectedStatus(e.target.value)}
            className="bg-white border border-slate-300 text-slate-700 text-xs rounded-lg px-2.5 py-1.5 focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="All">All Statuses</option>
            <option value="Actionable">Actionable Only</option>
            <option value="Overlapped">Overlapped Only</option>
          </select>

          <span className="text-xs text-slate-500 ml-2">
            Showing <strong className="text-slate-800">{filtered.length}</strong> of {recommendations.length}
          </span>
        </div>
      </div>

      {/* Table */}
      <div className="overflow-x-auto">
        <table className="w-full text-left text-xs">
          <thead className="bg-slate-100 text-slate-600 font-semibold border-b border-slate-200 uppercase tracking-wider text-[11px]">
            <tr>
              <th className="py-3 px-4">Status & Category</th>
              <th className="py-3 px-4">Resource & Recommendation</th>
              <th className="py-3 px-4">Current → Target SKU</th>
              <th className="py-3 px-4 text-right">Net Savings</th>
              <th className="py-3 px-4">Risk & Confidence</th>
              <th className="py-3 px-4 text-center">Action</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200">
            {filtered.length === 0 ? (
              <tr>
                <td colSpan={6} className="text-center py-8 text-slate-400">
                  No recommendations match the current search and filter criteria.
                </td>
              </tr>
            ) : (
              filtered.map((rec) => (
                <tr
                  key={rec.id}
                  className={`hover:bg-slate-50/80 transition-colors ${
                    rec.status === 'Overlapped' ? 'bg-slate-50/40 text-slate-500' : ''
                  }`}
                >
                  {/* Status & Category */}
                  <td className="py-3.5 px-4 space-y-1.5">
                    <div>{getStatusBadge(rec.status, rec.costs.exclusionReason)}</div>
                    <div>{getCategoryBadge(rec.category)}</div>
                  </td>

                  {/* Resource & Title */}
                  <td className="py-3.5 px-4 max-w-sm">
                    <div className="font-semibold text-slate-900 flex items-center space-x-1.5">
                      <span>{rec.resourceName}</span>
                      {rec.aiExplanation && (
                        <span title="Enriched with Azure AI Foundry" className="text-indigo-600">
                          <Sparkles className="w-3.5 h-3.5 inline" />
                        </span>
                      )}
                    </div>
                    <div className="text-slate-600 line-clamp-1 text-[11px] mt-0.5">{rec.title}</div>
                    <div className="text-[10px] text-slate-400 font-mono mt-0.5">
                      {rec.resourceGroup} • {rec.location}
                    </div>
                  </td>

                  {/* SKU Comparison */}
                  <td className="py-3.5 px-4">
                    <div className="text-slate-700 font-mono text-[11px]">
                      {rec.currentSku || '—'}
                    </div>
                    <div className="text-emerald-700 font-mono font-medium text-[11px] flex items-center mt-0.5">
                      <span className="text-slate-400 mr-1">→</span>
                      {rec.targetSku || '—'}
                    </div>
                  </td>

                  {/* Net Monthly Savings */}
                  <td className="py-3.5 px-4 text-right">
                    <div className={`text-sm font-bold ${
                      rec.status === 'Overlapped' ? 'text-slate-400 line-through' : 'text-emerald-600'
                    }`}>
                      ${rec.costs.netMonthlySavings.toFixed(2)}
                    </div>
                    <div className="text-[10px] text-slate-400">/ month</div>
                    {rec.costs.retainedStorageCost > 0 && (
                      <div className="text-[10px] text-purple-700 mt-0.5">
                        (${rec.costs.retainedStorageCost.toFixed(2)} disk kept)
                      </div>
                    )}
                  </td>

                  {/* Risk & Confidence */}
                  <td className="py-3.5 px-4 space-y-1">
                    <div>{getRiskBadge(rec.risk)}</div>
                    <div className="text-[10px] text-slate-500">
                      Confidence: <span className="font-semibold">{rec.confidence}</span>
                    </div>
                  </td>

                  {/* Action */}
                  <td className="py-3.5 px-4 text-center space-x-1">
                    <button
                      type="button"
                      onClick={() => onSelectRecommendation(rec)}
                      className="inline-flex items-center px-2.5 py-1.5 rounded-md text-xs font-medium bg-blue-50 text-blue-700 hover:bg-blue-100 transition-colors border border-blue-200"
                    >
                      <span>Details</span>
                      <ArrowUpRight className="w-3.5 h-3.5 ml-1" />
                    </button>

                    {rec.category === 'Scheduling' && (
                      <button
                        type="button"
                        onClick={() => onOpenSchedulePlanner(rec)}
                        title="Simulate custom schedule"
                        className="inline-flex items-center px-2 py-1.5 rounded-md text-xs font-medium bg-purple-50 text-purple-700 hover:bg-purple-100 transition-colors border border-purple-200"
                      >
                        <Calendar className="w-3.5 h-3.5" />
                      </button>
                    )}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
};
