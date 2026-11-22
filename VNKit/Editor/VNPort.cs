using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace VNKitEditor
{
    /// <summary>
    /// Port that reports "thread dropped on a port" and "thread dropped on empty space" to VNGraphView,
    /// so it can create frames / branches itself instead of GraphView's default behaviour.
    /// </summary>
    public class VNPort : Port
    {
        VNPort(Orientation orientation, Direction direction, Capacity capacity, Type type)
            : base(orientation, direction, capacity, type) { }

        public static VNPort Create(Orientation orientation, Direction direction, Capacity capacity, Type type)
        {
            var port = new VNPort(orientation, direction, capacity, type);
            port.m_EdgeConnector = new EdgeConnector<Edge>(new Listener());
            port.AddManipulator(port.m_EdgeConnector);
            return port;
        }

        class Listener : IEdgeConnectorListener
        {
            public void OnDrop(GraphView graphView, Edge edge)
            {
                var g = graphView as VNGraphView;
                if (g != null) g.ConnectFromDrop(edge);
            }

            public void OnDropOutsidePort(Edge edge, Vector2 position)
            {
                if (edge == null || edge.output == null) return;
                var g = edge.output.GetFirstAncestorOfType<VNGraphView>();
                if (g != null) g.CreateNodeFromDrop(edge, position);
            }
        }
    }
}
